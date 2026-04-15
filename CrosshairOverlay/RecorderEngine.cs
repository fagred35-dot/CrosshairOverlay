using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CrosshairOverlay
{
    internal class RecorderEngine : IDisposable
    {
        private Process? _recProc;
        private Process? _replayProc;
        private string _currentRecordPath = "";
        private string _replayTempDir = "";
        private System.Threading.Timer? _cleanupTimer;
        private bool _disposed;
        private int _segCounter; // monotonically increasing segment counter

        public bool IsRecording { get; private set; }
        public bool IsReplayActive { get; private set; }
        public DateTime RecordStart { get; private set; }
        public bool IsDownloading { get; private set; }
        public int DownloadPercent { get; private set; }

        // Config
        public string FfmpegPath { get; set; } = "ffmpeg";
        public string OutputDir { get; set; } = "";
        public int Fps { get; set; } = 30;
        public int Crf { get; set; } = 23;
        public string Preset { get; set; } = "fast";
        public string AudioDev { get; set; } = "";
        public string MicDev { get; set; } = "";
        public bool UseMic { get; set; }
        public int ReplaySec { get; set; } = 120;
        public List<string> AudioApps { get; set; } = new();

        public event Action<string>? Saved;
        public event Action<string>? ReplaySaved;
        public event Action<string>? OnError;

        public RecorderEngine()
        {
            if (string.IsNullOrEmpty(OutputDir))
                OutputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    "CrosshairOverlay");
        }

        public void Init()
        {
            if (TestFFmpeg(FfmpegPath)) return;
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
                @"C:\ffmpeg\bin\ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "ffmpeg", "bin", "ffmpeg.exe"),
                "ffmpeg"
            };
            foreach (var c in candidates)
            {
                if (TestFFmpeg(c)) { FfmpegPath = c; return; }
            }
        }

        private static bool TestFFmpeg(string path)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(path, "-version")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                });
                p?.WaitForExit(3000);
                return p?.ExitCode == 0;
            }
            catch { return false; }
        }

        public bool HasFFmpeg() => TestFFmpeg(FfmpegPath);

        /// <summary>Download FFmpeg essentials to app directory.</summary>
        public async Task<bool> DownloadFFmpegAsync()
        {
            if (IsDownloading) return false;
            IsDownloading = true;
            DownloadPercent = 0;
            try
            {
                string destDir = AppDomain.CurrentDomain.BaseDirectory;
                string destExe = Path.Combine(destDir, "ffmpeg.exe");
                if (File.Exists(destExe)) { FfmpegPath = destExe; return true; }

                string zipPath = Path.Combine(Path.GetTempPath(), "ffmpeg_dl.zip");
                string url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(10);
                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();

                long total = resp.Content.Headers.ContentLength ?? 0;
                using (var src = await resp.Content.ReadAsStreamAsync())
                using (var dst = File.Create(zipPath))
                {
                    byte[] buf = new byte[81920];
                    long done = 0;
                    int read;
                    while ((read = await src.ReadAsync(buf, 0, buf.Length)) > 0)
                    {
                        await dst.WriteAsync(buf, 0, read);
                        done += read;
                        if (total > 0) DownloadPercent = (int)(done * 100 / total);
                    }
                }
                DownloadPercent = 100;

                // Extract ffmpeg.exe from zip
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    var entry = zip.Entries.FirstOrDefault(e =>
                        e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                    if (entry != null)
                    {
                        entry.ExtractToFile(destExe, true);
                    }
                }

                try { File.Delete(zipPath); } catch { }

                if (File.Exists(destExe))
                {
                    FfmpegPath = destExe;
                    return true;
                }
                return false;
            }
            catch (Exception ex) { OnError?.Invoke($"Download: {ex.Message}"); return false; }
            finally { IsDownloading = false; }
        }

        public List<string> ListAudioDevices()
        {
            var result = new List<string>();
            try
            {
                using var p = Process.Start(new ProcessStartInfo(FfmpegPath,
                    "-list_devices true -f dshow -i dummy")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardError = true
                });
                if (p == null) return result;
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit(5000);

                bool audio = false;
                foreach (var line in err.Split('\n'))
                {
                    if (line.Contains("DirectShow audio devices")) audio = true;
                    else if (line.Contains("DirectShow video devices")) audio = false;
                    else if (audio)
                    {
                        var m = Regex.Match(line, "\"(.+?)\"");
                        if (m.Success && !line.Contains("Alternative name"))
                            result.Add(m.Groups[1].Value);
                    }
                }
            }
            catch { }
            return result;
        }

        private string BuildArgs(string output, bool isSegment = false)
        {
            var sb = new StringBuilder();
            sb.Append($"-f gdigrab -framerate {Fps} -i desktop ");

            if (!string.IsNullOrEmpty(AudioDev))
                sb.Append($"-f dshow -i audio=\"{AudioDev}\" ");
            if (UseMic && !string.IsNullOrEmpty(MicDev))
                sb.Append($"-f dshow -i audio=\"{MicDev}\" ");

            sb.Append($"-c:v libx264 -preset {Preset} -crf {Crf} ");
            sb.Append("-pix_fmt yuv420p ");

            bool hasAudio = !string.IsNullOrEmpty(AudioDev);
            bool hasMic = UseMic && !string.IsNullOrEmpty(MicDev);

            if (hasAudio || hasMic)
            {
                sb.Append("-c:a aac -b:a 192k ");
                if (hasAudio && hasMic)
                    sb.Append("-filter_complex \"[1:a][2:a]amix=inputs=2[a]\" -map 0:v -map \"[a]\" ");
            }

            if (isSegment)
            {
                sb.Append("-f segment -segment_time 10 -reset_timestamps 1 ");
                sb.Append("-segment_format mpegts ");
            }

            sb.Append($"-y \"{output}\"");
            return sb.ToString();
        }

        // ═══════════════════════════════════════
        //  Recording
        // ═══════════════════════════════════════

        public bool StartRecord()
        {
            if (IsRecording) return false;
            if (!HasFFmpeg()) return false;
            Directory.CreateDirectory(OutputDir);
            _currentRecordPath = Path.Combine(OutputDir,
                $"Rec_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4");

            _recProc = LaunchFF(BuildArgs(_currentRecordPath));
            if (_recProc == null) return false;
            IsRecording = true;
            RecordStart = DateTime.Now;
            return true;
        }

        public string? StopRecord()
        {
            if (!IsRecording || _recProc == null) return null;
            StopFF(_recProc);
            _recProc = null;
            IsRecording = false;

            if (File.Exists(_currentRecordPath))
            {
                Saved?.Invoke(_currentRecordPath);
                return _currentRecordPath;
            }
            return null;
        }

        // ═══════════════════════════════════════
        //  Replay Buffer
        // ═══════════════════════════════════════

        public bool StartReplay()
        {
            if (IsReplayActive) return false;
            if (!HasFFmpeg()) return false;

            _replayTempDir = Path.Combine(Path.GetTempPath(),
                "CrosshairReplay_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_replayTempDir);
            _segCounter = 0;

            string pattern = Path.Combine(_replayTempDir, $"seg_{_segCounter:D6}_%04d.ts");
            _replayProc = LaunchFF(BuildArgs(pattern, isSegment: true));
            if (_replayProc == null) return false;

            IsReplayActive = true;
            _cleanupTimer = new System.Threading.Timer(_ => CleanupSegments(),
                null, 5000, 5000);
            return true;
        }

        public void StopReplay()
        {
            if (!IsReplayActive) return;
            _cleanupTimer?.Dispose();
            _cleanupTimer = null;

            if (_replayProc != null) { StopFF(_replayProc); _replayProc = null; }
            IsReplayActive = false;

            try { if (Directory.Exists(_replayTempDir)) Directory.Delete(_replayTempDir, true); }
            catch { }
        }

        public string? SaveReplay()
        {
            if (!IsReplayActive) return null;

            // Stop current recording
            if (_replayProc != null) { StopFF(_replayProc); _replayProc = null; }

            var segments = Directory.GetFiles(_replayTempDir, "seg_*.ts")
                .OrderBy(f => f).ToArray();

            if (segments.Length == 0) { RestartReplay(); return null; }

            int segCount = Math.Max(1, ReplaySec / 10 + 1);
            var toConcat = segments.TakeLast(segCount).ToArray();

            Directory.CreateDirectory(OutputDir);
            string output = Path.Combine(OutputDir,
                $"Replay_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4");

            string listFile = Path.Combine(_replayTempDir, "concat.txt");
            File.WriteAllLines(listFile,
                toConcat.Select(f => $"file '{f.Replace('\\', '/')}'"));

            try
            {
                using var p = Process.Start(new ProcessStartInfo(FfmpegPath,
                    $"-f concat -safe 0 -i \"{listFile}\" -c copy -y \"{output}\"")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardError = true
                });
                p?.WaitForExit(30000);
            }
            catch (Exception ex) { OnError?.Invoke(ex.Message); }

            // Clean old segments and restart
            foreach (var f in segments)
                try { File.Delete(f); } catch { }

            RestartReplay();

            if (File.Exists(output))
            {
                ReplaySaved?.Invoke(output);
                return output;
            }
            return null;
        }

        private void RestartReplay()
        {
            _segCounter++;
            string pattern = Path.Combine(_replayTempDir, $"seg_{_segCounter:D6}_%04d.ts");
            _replayProc = LaunchFF(BuildArgs(pattern, isSegment: true));
            _cleanupTimer = new System.Threading.Timer(_ => CleanupSegments(),
                null, 5000, 5000);
        }

        private void CleanupSegments()
        {
            try
            {
                if (!Directory.Exists(_replayTempDir)) return;
                var files = Directory.GetFiles(_replayTempDir, "seg_*.ts")
                    .OrderBy(f => f).ToArray();
                int keep = ReplaySec / 10 + 2;
                if (files.Length > keep)
                {
                    for (int i = 0; i < files.Length - keep; i++)
                        try { File.Delete(files[i]); } catch { }
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════
        //  Thumbnails
        // ═══════════════════════════════════════

        public string? GenerateThumbnail(string videoPath, int width, int height)
        {
            string thumbDir = Path.Combine(OutputDir, ".thumbs");
            Directory.CreateDirectory(thumbDir);
            string thumbPath = Path.Combine(thumbDir,
                Path.GetFileNameWithoutExtension(videoPath) + ".jpg");

            if (File.Exists(thumbPath)) return thumbPath;

            try
            {
                using var p = Process.Start(new ProcessStartInfo(FfmpegPath,
                    $"-i \"{videoPath}\" -ss 00:00:01 -vframes 1 -s {width}x{height} -y \"{thumbPath}\"")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardError = true
                });
                p?.WaitForExit(10000);
                return File.Exists(thumbPath) ? thumbPath : null;
            }
            catch { return null; }
        }

        // ═══════════════════════════════════════
        //  Process helpers
        // ═══════════════════════════════════════

        private Process? LaunchFF(string args)
        {
            try
            {
                return Process.Start(new ProcessStartInfo(FfmpegPath, args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardError = true
                });
            }
            catch (Exception ex) { OnError?.Invoke($"FFmpeg: {ex.Message}"); return null; }
        }

        private static void StopFF(Process p)
        {
            try
            {
                if (!p.HasExited)
                {
                    try { p.StandardInput.Write("q"); p.StandardInput.Flush(); } catch { }
                    p.WaitForExit(5000);
                    if (!p.HasExited) p.Kill();
                }
                p.Dispose();
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (IsRecording) StopRecord();
            if (IsReplayActive) StopReplay();
            _cleanupTimer?.Dispose();
        }
    }
}
