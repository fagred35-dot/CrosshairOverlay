using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrosshairOverlay
{
    /// <summary>
    /// Self-updater backed by GitHub Releases.
    ///
    /// Flow:
    ///   FetchLatestAsync → user confirms → DownloadAsync (with progress + cancel) →
    ///   VerifyExecutable → ScheduleSwapAndRestart → Application.Exit
    ///
    /// The replacement step is performed by a tiny PID-aware .cmd helper that waits
    /// for our process to exit, moves the freshly downloaded `.exe.update` over the
    /// running binary, then relaunches the app and deletes itself.
    /// </summary>
    internal static class Updater
    {
        private const string GitHubRepo = OverlayForm.GITHUB_REPO_PUBLIC;
        private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(20);

        // Single reusable HttpClient — separate from anything else in the app.
        private static readonly HttpClient _http = CreateClient();

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = NetworkTimeout };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("CrosshairOverlay/" + OverlayForm.APP_VERSION);
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return c;
        }

        public sealed class ReleaseInfo
        {
            public string Tag = "";
            public string DisplayVersion = "";
            public Version? Version;
            public string DownloadUrl = "";
            public string AssetName = "";
            public long Size;
            public string? Sha256;
            public string? Notes;
            public bool IsPrerelease;
        }

        /// <summary>Returns Version("2.3.0") from "v2.3.0", "2.3.0", "2.3.0-beta1" etc.</summary>
        public static Version? ParseVersion(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim().TrimStart('v', 'V');
            // Strip pre-release / build metadata: 2.3.0-beta.2+sha → 2.3.0
            int dash = s.IndexOfAny(new[] { '-', '+' });
            if (dash > 0) s = s.Substring(0, dash);
            // Pad short forms ("2.3" → "2.3.0").
            int dots = 0;
            foreach (var ch in s) if (ch == '.') dots++;
            for (int i = dots; i < 2; i++) s += ".0";
            return Version.TryParse(s, out var v) ? v : null;
        }

        public static async Task<ReleaseInfo?> FetchLatestAsync(bool includePrerelease, CancellationToken ct)
        {
            string url = includePrerelease
                ? $"https://api.github.com/repos/{GitHubRepo}/releases?per_page=5"
                : $"https://api.github.com/repos/{GitHubRepo}/releases/latest";

            var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            JsonElement root = doc.RootElement;
            JsonElement chosen;
            if (includePrerelease)
            {
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return null;
                chosen = root[0];
                // /releases doesn't filter drafts on the server; first non-draft wins.
                foreach (var rel in root.EnumerateArray())
                {
                    if (rel.TryGetProperty("draft", out var dr) && dr.GetBoolean()) continue;
                    chosen = rel;
                    break;
                }
            }
            else
            {
                chosen = root;
            }

            return ParseReleaseElement(chosen);
        }

        private static ReleaseInfo? ParseReleaseElement(JsonElement rel)
        {
            string tag = rel.TryGetProperty("tag_name", out var tg) ? tg.GetString() ?? "" : "";
            string name = rel.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
            bool prerelease = rel.TryGetProperty("prerelease", out var pr) && pr.GetBoolean();
            string body = rel.TryGetProperty("body", out var bd) ? bd.GetString() ?? "" : "";

            if (!rel.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;

            // Prefer a singlefile-published exe with a sensible name; fall back to first .exe.
            string? url = null, asset = null;
            long size = 0;
            foreach (var a in assets.EnumerateArray())
            {
                string aName = a.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                if (!aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                long aSize = a.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                string aUrl = a.TryGetProperty("browser_download_url", out var bu) ? bu.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(aUrl)) continue;
                if (aName.IndexOf("CrosshairOverlay", StringComparison.OrdinalIgnoreCase) >= 0
                    || url == null)
                {
                    url = aUrl; asset = aName; size = aSize;
                }
            }
            if (url == null || asset == null) return null;

            // Optional: a sibling "<name>.sha256" or "SHA256SUMS" asset, or hex hash inline in body.
            string? sha256 = ExtractSha256(body, asset);

            return new ReleaseInfo
            {
                Tag = tag,
                DisplayVersion = string.IsNullOrEmpty(name) ? tag : name,
                Version = ParseVersion(tag),
                DownloadUrl = url,
                AssetName = asset,
                Size = size,
                Sha256 = sha256,
                Notes = body,
                IsPrerelease = prerelease,
            };
        }

        private static string? ExtractSha256(string body, string assetName)
        {
            // 1) "sha256: abcdef…" anywhere in the release notes.
            var m = Regex.Match(body, @"sha-?256[\s:]+([0-9a-fA-F]{64})", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.ToLowerInvariant();
            // 2) "abcdef…  CrosshairOverlay.exe" line (sha256sum format).
            var asset = Regex.Escape(assetName);
            m = Regex.Match(body, @"([0-9a-fA-F]{64})\s+\*?" + asset, RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.ToLowerInvariant();
            return null;
        }

        public static bool IsNewerThanCurrent(ReleaseInfo r)
        {
            var current = ParseVersion(OverlayForm.APP_VERSION);
            if (current == null || r.Version == null) return false;
            return r.Version > current;
        }

        public static async Task DownloadAsync(string url, string destPath,
            IProgress<(long received, long total)>? progress, CancellationToken ct)
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            long? total = resp.Content.Headers.ContentLength;
            using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true);

            byte[] buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                received += read;
                progress?.Report((received, total ?? -1));
            }
        }

        /// <summary>
        /// Sanity-check the downloaded file: non-empty, expected size (if known),
        /// starts with a Win32 PE/MZ header, and matches the SHA-256 if the release
        /// advertises one.
        /// </summary>
        public static bool VerifyExecutable(string path, long expectedSize, string? expectedSha256, out string error)
        {
            error = "";
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.Length == 0) { error = "Downloaded file is empty"; return false; }
                if (expectedSize > 0 && fi.Length != expectedSize)
                {
                    error = $"Size mismatch: expected {expectedSize}, got {fi.Length}";
                    return false;
                }

                using (var fs = File.OpenRead(path))
                {
                    int b0 = fs.ReadByte(), b1 = fs.ReadByte();
                    if (b0 != 'M' || b1 != 'Z') { error = "Not a Windows executable (missing MZ header)"; return false; }
                }

                if (!string.IsNullOrEmpty(expectedSha256))
                {
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    using var fs = File.OpenRead(path);
                    var hash = sha.ComputeHash(fs);
                    var hex = Convert.ToHexString(hash).ToLowerInvariant();
                    if (!string.Equals(hex, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        error = "SHA-256 mismatch — file may be corrupted";
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Writes a small PID-aware .cmd helper next to the temp file, launches it, and
        /// returns. The caller should immediately exit the application so the helper can
        /// replace the .exe.
        /// </summary>
        public static void ScheduleSwapAndRestart(string updatedExePath, string targetExePath)
        {
            int pid = Environment.ProcessId;
            string log = Path.Combine(Path.GetTempPath(), "crosshair_update.log");
            string bat = Path.Combine(Path.GetTempPath(), "crosshair_update.cmd");

            string targetEsc = targetExePath.Replace("\"", "\"\"");
            string updateEsc = updatedExePath.Replace("\"", "\"\"");
            string logEsc = log.Replace("\"", "\"\"");

            string script =
                "@echo off\r\n" +
                "setlocal\r\n" +
                "set \"LOG=" + logEsc + "\"\r\n" +
                $"echo [%date% %time%] Crosshair Overlay updater pid={pid} > \"%LOG%\"\r\n" +
                "set /a TRIES=0\r\n" +
                ":waitloop\r\n" +
                $"tasklist /FI \"PID eq {pid}\" /NH 2>nul | findstr /B /C:\"CrosshairOverlay\" >nul\r\n" +
                "if errorlevel 1 goto :swap\r\n" +
                "set /a TRIES+=1\r\n" +
                "if %TRIES% GEQ 60 goto :timeout\r\n" +
                "ping -n 2 127.0.0.1 >nul\r\n" +
                "goto :waitloop\r\n" +
                ":timeout\r\n" +
                "echo [error] target process still running after 60s >> \"%LOG%\"\r\n" +
                "goto :end\r\n" +
                ":swap\r\n" +
                "ping -n 2 127.0.0.1 >nul\r\n" +
                $"move /Y \"{updateEsc}\" \"{targetEsc}\" >> \"%LOG%\" 2>&1\r\n" +
                "if errorlevel 1 (\r\n" +
                "  echo [error] move failed - check write permissions >> \"%LOG%\"\r\n" +
                "  goto :end\r\n" +
                ")\r\n" +
                $"start \"\" \"{targetEsc}\"\r\n" +
                ":end\r\n" +
                "(goto) 2>nul & del \"%~f0\"\r\n";

            File.WriteAllText(bat, script);

            var psi = new ProcessStartInfo
            {
                FileName = bat,
                UseShellExecute = true,        // required for .cmd
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            };
            Process.Start(psi);
        }

        /// <summary>Removes a stale `.update` file from a previous run that didn't finish swapping.</summary>
        public static void CleanupLeftovers()
        {
            try
            {
                var leftover = Application.ExecutablePath + ".update";
                if (File.Exists(leftover))
                {
                    // If it's older than 10 minutes, remove it; otherwise the helper is probably still running.
                    if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(leftover)).TotalMinutes > 10)
                        File.Delete(leftover);
                }
            }
            catch { /* best-effort */ }
        }
    }
}
