import discord
from discord.ext import commands

from utils.ai_api import ask_openrouter


class AIChat(commands.Cog):
    def __init__(self, bot: commands.Bot) -> None:
        self.bot = bot
        self.histories: dict[int, list[dict[str, str]]] = {}

    @commands.Cog.listener()
    async def on_message(self, message: discord.Message) -> None:
        if message.author.bot:
            return

        content = message.content.strip()
        if not content.lower().startswith("грай"):
            return

        prompt = content[4:].lstrip(" ,.!?:;-—\n\t")
        if not prompt:
            await message.reply("Здравия желаю! Сформулируй приказ после позывного «Грай».")
            return

        history = self.histories.setdefault(message.channel.id, [])
        history.append({"role": "user", "content": prompt})
        self.histories[message.channel.id] = history[-10:]

        async with message.channel.typing():
            answer = await ask_openrouter(self.histories[message.channel.id])

        history = self.histories.setdefault(message.channel.id, [])
        history.append({"role": "assistant", "content": answer})
        self.histories[message.channel.id] = history[-10:]

        await message.reply(answer[:2000])


async def setup(bot: commands.Bot) -> None:
    await bot.add_cog(AIChat(bot))
