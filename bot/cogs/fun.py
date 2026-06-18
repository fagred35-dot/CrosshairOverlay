import random

import aiohttp
import discord
from discord import app_commands
from discord.ext import commands

from utils.database import get_balance, update_balance


class Fun(commands.Cog):
    def __init__(self, bot: commands.Bot) -> None:
        self.bot = bot

    @app_commands.command(name="coinflip", description="Орёл/решка со ставкой")
    @app_commands.describe(ставка="Сколько монет поставить")
    async def coinflip(self, interaction: discord.Interaction, ставка: int) -> None:
        if ставка <= 0:
            await interaction.response.send_message("Никак нет: ставка должна быть больше нуля.", ephemeral=True)
            return

        balance = get_balance(interaction.user.id)
        if balance < ставка:
            await interaction.response.send_message("Никак нет: монет не хватает.", ephemeral=True)
            return

        update_balance(interaction.user.id, -ставка)
        side = random.choice(["орёл", "решка"])
        won = random.choice([True, False])

        if won:
            new_balance = update_balance(interaction.user.id, ставка * 2)
            await interaction.response.send_message(
                f"Монета показала: {side}. Так точно, выигрыш x2: {ставка * 2} монет. Баланс: {new_balance}."
            )
        else:
            new_balance = get_balance(interaction.user.id)
            await interaction.response.send_message(
                f"Монета показала: {side}. Никак нет, ставка ушла в казну. Баланс: {new_balance}."
            )

    @app_commands.command(name="rps", description="Камень, ножницы, бумага против бота")
    @app_commands.describe(выбор="Твой выбор")
    @app_commands.choices(
        выбор=[
            app_commands.Choice(name="камень", value="камень"),
            app_commands.Choice(name="ножницы", value="ножницы"),
            app_commands.Choice(name="бумага", value="бумага"),
        ]
    )
    async def rps(self, interaction: discord.Interaction, выбор: app_commands.Choice[str]) -> None:
        user_choice = выбор.value
        bot_choice = random.choice(["камень", "ножницы", "бумага"])

        if user_choice == bot_choice:
            result = "ничья, бойцы разошлись без потерь"
        elif (
            (user_choice == "камень" and bot_choice == "ножницы")
            or (user_choice == "ножницы" and bot_choice == "бумага")
            or (user_choice == "бумага" and bot_choice == "камень")
        ):
            result = "победа за тобой, боец"
        else:
            result = "победа за Граем, тренируй строевую"

        await interaction.response.send_message(
            f"Ты выбрал: {user_choice}. Грай выбрал: {bot_choice}. Докладываю: {result}."
        )

    @app_commands.command(name="meme", description="Случайный мем из Reddit")
    async def meme(self, interaction: discord.Interaction) -> None:
        await interaction.response.defer()

        try:
            timeout = aiohttp.ClientTimeout(total=15)
            headers = {"User-Agent": "GrayDiscordBot/1.0"}
            async with aiohttp.ClientSession(timeout=timeout, headers=headers) as session:
                async with session.get("https://www.reddit.com/r/memes/random.json") as response:
                    if response.status >= 400:
                        await interaction.followup.send(f"Докладываю: Reddit не отвечает как надо ({response.status}).")
                        return

                    data = await response.json(content_type=None)

            post = data[0]["data"]["children"][0]["data"]
            if post.get("over_18"):
                await interaction.followup.send("Никак нет: выпал NSFW-мем, в казарму такое не тащим.")
                return

            embed = discord.Embed(title=post.get("title", "Мем с передовой"), url=f"https://reddit.com{post.get('permalink', '')}")
            embed.set_image(url=post.get("url"))
            embed.set_footer(text="Источник: r/memes")
            await interaction.followup.send(embed=embed)
        except (aiohttp.ClientError, KeyError, IndexError, TimeoutError, ValueError) as error:
            await interaction.followup.send(f"Докладываю: мем не доставлен: {error}")


async def setup(bot: commands.Bot) -> None:
    await bot.add_cog(Fun(bot))
