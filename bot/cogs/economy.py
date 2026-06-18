import random
import time

import discord
from discord import app_commands
from discord.ext import commands

from utils.database import get_balance, get_last_daily, set_last_daily, update_balance


DAILY_COOLDOWN = 24 * 60 * 60


class Economy(commands.Cog):
    def __init__(self, bot: commands.Bot) -> None:
        self.bot = bot

    @app_commands.command(name="daily", description="Получить ежедневные монеты")
    async def daily(self, interaction: discord.Interaction) -> None:
        user_id = interaction.user.id
        now = int(time.time())
        last_daily = get_last_daily(user_id)

        if last_daily is not None and now - last_daily < DAILY_COOLDOWN:
            remaining = DAILY_COOLDOWN - (now - last_daily)
            hours = remaining // 3600
            minutes = (remaining % 3600) // 60
            await interaction.response.send_message(
                f"Никак нет: довольствие уже выдано. Возвращайся через {hours} ч. {minutes} мин.",
                ephemeral=True,
            )
            return

        reward = random.randint(50, 200)
        balance = update_balance(user_id, reward)
        set_last_daily(user_id, now)
        await interaction.response.send_message(f"Так точно: начислено {reward} монет. Баланс: {balance}.")

    @app_commands.command(name="balance", description="Показать баланс")
    @app_commands.describe(участник="Чей баланс показать")
    async def balance(
        self,
        interaction: discord.Interaction,
        участник: discord.Member | None = None,
    ) -> None:
        target = участник or interaction.user
        balance = get_balance(target.id)
        await interaction.response.send_message(f"Докладываю: баланс {target.mention}: {balance} монет.")


async def setup(bot: commands.Bot) -> None:
    await bot.add_cog(Economy(bot))
