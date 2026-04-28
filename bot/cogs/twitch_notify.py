import os
import time
from typing import Any

import aiohttp
import discord
from discord import app_commands
from discord.ext import commands, tasks

from utils.database import (
    add_twitch_channel,
    get_all_twitch_channels,
    remove_twitch_channel,
    update_twitch_status,
)


class TwitchNotify(commands.Cog):
    def __init__(self, bot: commands.Bot) -> None:
        self.bot = bot
        self._token: str | None = None
        self._token_expires_at = 0
        self.check_twitch_streams.start()

    def cog_unload(self) -> None:
        self.check_twitch_streams.cancel()

    def _api_ready(self) -> bool:
        return bool(os.getenv("TWITCH_CLIENT_ID") and os.getenv("TWITCH_CLIENT_SECRET"))

    async def _get_token(self, session: aiohttp.ClientSession) -> str | None:
        if not self._api_ready():
            return None

        if self._token and time.time() < self._token_expires_at - 60:
            return self._token

        client_id = os.getenv("TWITCH_CLIENT_ID")
        client_secret = os.getenv("TWITCH_CLIENT_SECRET")
        url = "https://id.twitch.tv/oauth2/token"
        payload = {
            "client_id": client_id,
            "client_secret": client_secret,
            "grant_type": "client_credentials",
        }

        async with session.post(url, data=payload) as response:
            if response.status >= 400:
                return None

            data = await response.json()
            self._token = data.get("access_token")
            self._token_expires_at = int(time.time()) + int(data.get("expires_in", 0))
            return self._token

    async def _fetch_stream(self, session: aiohttp.ClientSession, channel_name: str) -> dict[str, Any] | None:
        token = await self._get_token(session)
        client_id = os.getenv("TWITCH_CLIENT_ID")
        if not token or not client_id:
            return None

        headers = {
            "Client-ID": client_id,
            "Authorization": f"Bearer {token}",
        }
        params = {"user_login": channel_name}

        async with session.get("https://api.twitch.tv/helix/streams", headers=headers, params=params) as response:
            if response.status >= 400:
                return None

            data = await response.json()
            streams = data.get("data", [])
            return streams[0] if streams else None

    @app_commands.command(name="twitch_add", description="Добавить Twitch-канал для уведомлений")
    @app_commands.describe(канал="Название Twitch-канала")
    async def twitch_add(self, interaction: discord.Interaction, канал: str) -> None:
        if interaction.guild is None or interaction.channel is None:
            await interaction.response.send_message("Никак нет: команда работает только на сервере.", ephemeral=True)
            return

        if not self._api_ready():
            await interaction.response.send_message("Twitch API не настроен", ephemeral=True)
            return

        channel_name = канал.lower().strip().lstrip("@")
        add_twitch_channel(channel_name, interaction.channel.id, interaction.guild.id)
        await interaction.response.send_message(
            f"Так точно: Twitch-канал `{channel_name}` добавлен. Уведомления будут здесь."
        )

    @app_commands.command(name="twitch_remove", description="Убрать Twitch-канал из уведомлений")
    @app_commands.describe(канал="Название Twitch-канала")
    async def twitch_remove(self, interaction: discord.Interaction, канал: str) -> None:
        if interaction.guild is None:
            await interaction.response.send_message("Никак нет: команда работает только на сервере.", ephemeral=True)
            return

        channel_name = канал.lower().strip().lstrip("@")
        removed = remove_twitch_channel(channel_name, interaction.guild.id)
        if removed:
            await interaction.response.send_message(f"Так точно: `{channel_name}` снят с наблюдения.")
        else:
            await interaction.response.send_message(f"Докладываю: `{channel_name}` не был в списке.", ephemeral=True)

    @app_commands.command(name="twitch_list", description="Список отслеживаемых Twitch-каналов")
    async def twitch_list(self, interaction: discord.Interaction) -> None:
        if interaction.guild is None:
            await interaction.response.send_message("Никак нет: команда работает только на сервере.", ephemeral=True)
            return

        channels = get_all_twitch_channels(interaction.guild.id)
        if not channels:
            await interaction.response.send_message("Докладываю: Twitch-каналы пока не отслеживаются.")
            return

        lines = [f"`{row['channel_name']}` → <#{row['discord_channel_id']}>" for row in channels]
        await interaction.response.send_message("Отслеживаемые Twitch-каналы:\n" + "\n".join(lines))

    @tasks.loop(seconds=60)
    async def check_twitch_streams(self) -> None:
        rows = get_all_twitch_channels()
        if not rows:
            return

        if not self._api_ready():
            print("Twitch API не настроен")
            return

        timeout = aiohttp.ClientTimeout(total=30)
        async with aiohttp.ClientSession(timeout=timeout) as session:
            for row in rows:
                channel_name = row["channel_name"]
                guild_id = row["guild_id"]
                stream = await self._fetch_stream(session, channel_name)

                if stream is None:
                    if row["is_live"]:
                        update_twitch_status(channel_name, guild_id, False, None)
                    continue

                stream_id = str(stream.get("id"))
                was_live = bool(row["is_live"])
                last_stream_id = row.get("last_stream_id")
                update_twitch_status(channel_name, guild_id, True, stream_id)

                if was_live and last_stream_id == stream_id:
                    continue

                discord_channel = self.bot.get_channel(row["discord_channel_id"])
                if not isinstance(discord_channel, discord.abc.Messageable):
                    continue

                title = stream.get("title", "Стрим начался")
                game_name = stream.get("game_name") or "категория не указана"
                url = f"https://www.twitch.tv/{channel_name}"
                await discord_channel.send(
                    f"Докладываю: `{channel_name}` вышел в эфир!\n"
                    f"**{title}**\n"
                    f"Категория: {game_name}\n"
                    f"{url}"
                )

    @check_twitch_streams.before_loop
    async def before_check_twitch_streams(self) -> None:
        await self.bot.wait_until_ready()


async def setup(bot: commands.Bot) -> None:
    await bot.add_cog(TwitchNotify(bot))
