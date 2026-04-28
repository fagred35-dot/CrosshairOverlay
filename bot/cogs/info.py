import datetime as dt

import discord
from discord import app_commands
from discord.ext import commands


class Info(commands.Cog):
    def __init__(self, bot: commands.Bot) -> None:
        self.bot = bot

    @app_commands.command(name="serverinfo", description="Информация о сервере")
    async def serverinfo(self, interaction: discord.Interaction) -> None:
        guild = interaction.guild
        if guild is None:
            await interaction.response.send_message("Никак нет: команда работает только на сервере.", ephemeral=True)
            return

        created = discord.utils.format_dt(guild.created_at, style="F")
        embed = discord.Embed(title=f"Сводка по серверу: {guild.name}", color=discord.Color.green())
        embed.add_field(name="ID", value=str(guild.id), inline=True)
        embed.add_field(name="Владелец", value=guild.owner.mention if guild.owner else "Не могу знать", inline=True)
        embed.add_field(name="Участников", value=str(guild.member_count or 0), inline=True)
        embed.add_field(name="Каналов", value=str(len(guild.channels)), inline=True)
        embed.add_field(name="Ролей", value=str(len(guild.roles)), inline=True)
        embed.add_field(name="Создан", value=created, inline=False)

        if guild.icon:
            embed.set_thumbnail(url=guild.icon.url)

        await interaction.response.send_message(embed=embed)

    @app_commands.command(name="userinfo", description="Информация об участнике")
    @app_commands.describe(участник="Кого показать")
    async def userinfo(
        self,
        interaction: discord.Interaction,
        участник: discord.Member | None = None,
    ) -> None:
        member = участник or interaction.user
        if not isinstance(member, discord.Member):
            await interaction.response.send_message("Никак нет: участник не найден.", ephemeral=True)
            return

        joined_at = discord.utils.format_dt(member.joined_at, style="F") if member.joined_at else "Не могу знать"
        created_at = discord.utils.format_dt(member.created_at, style="F")
        roles = [role.mention for role in member.roles if role.name != "@everyone"]
        roles_text = ", ".join(roles[-10:]) if roles else "Без ролей"

        embed = discord.Embed(title=f"Личное дело: {member}", color=member.color)
        embed.add_field(name="ID", value=str(member.id), inline=True)
        embed.add_field(name="Бот", value="Да" if member.bot else "Нет", inline=True)
        embed.add_field(name="Аккаунт создан", value=created_at, inline=False)
        embed.add_field(name="На сервере с", value=joined_at, inline=False)
        embed.add_field(name="Роли", value=roles_text, inline=False)
        embed.set_thumbnail(url=member.display_avatar.url)

        await interaction.response.send_message(embed=embed)

    @app_commands.command(name="avatar", description="Показать аватар участника")
    @app_commands.describe(участник="Чей аватар показать")
    async def avatar(
        self,
        interaction: discord.Interaction,
        участник: discord.Member | None = None,
    ) -> None:
        member = участник or interaction.user
        embed = discord.Embed(title=f"Аватар: {member}", color=discord.Color.blurple())
        embed.set_image(url=member.display_avatar.url)
        await interaction.response.send_message(embed=embed)

    @app_commands.command(name="ping", description="Проверить задержку бота")
    async def ping(self, interaction: discord.Interaction) -> None:
        latency_ms = round(self.bot.latency * 1000)
        await interaction.response.send_message(f"Докладываю: задержка {latency_ms} мс.")


async def setup(bot: commands.Bot) -> None:
    await bot.add_cog(Info(bot))
