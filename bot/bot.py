import os
from pathlib import Path

import discord
from discord.ext import commands

from utils.database import init_db


# Главный класс бота: здесь поднимаем базу, загружаем коги и синхронизируем slash-команды.
class GrayBot(commands.Bot):
    def __init__(self) -> None:
        intents = discord.Intents.default()
        intents.guilds = True
        intents.members = True
        intents.messages = True
        intents.message_content = True

        super().__init__(command_prefix="!", intents=intents)

    async def setup_hook(self) -> None:
        init_db()
        await self._load_cogs()
        synced_commands = await self.tree.sync()
        print(f"Синхронизировано slash-команд: {len(synced_commands)}")

    async def _load_cogs(self) -> None:
        cogs_dir = Path(__file__).parent / "cogs"
        for cog_file in cogs_dir.glob("*.py"):
            if cog_file.name.startswith("_"):
                continue

            extension = f"cogs.{cog_file.stem}"
            try:
                await self.load_extension(extension)
                print(f"Ког загружен: {extension}")
            except Exception as error:
                print(f"Не удалось загрузить ког {extension}: {error}")

    async def on_ready(self) -> None:
        if self.user is None:
            return

        print(f"Грай на связи: {self.user} (ID: {self.user.id})")


token = os.getenv("DISCORD_TOKEN")
if not token:
    raise SystemExit("DISCORD_TOKEN не указан в переменных окружения")

bot = GrayBot()
bot.run(token)
