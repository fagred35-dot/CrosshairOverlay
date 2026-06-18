import datetime as dt

import discord
from discord import app_commands
from discord.ext import commands

from utils.database import add_warn, get_warnings


class Moderation(commands.Cog):
    def __init__(self, bot: commands.Bot) -> None:
        self.bot = bot

    @app_commands.command(name="ban", description="Забанить участника")
    @app_commands.describe(участник="Кого забанить", причина="Причина бана")
    @app_commands.checks.has_permissions(ban_members=True)
    @app_commands.checks.bot_has_permissions(ban_members=True)
    async def ban(
        self,
        interaction: discord.Interaction,
        участник: discord.Member,
        причина: str = "Причина не указана",
    ) -> None:
        if участник == interaction.user:
            await interaction.response.send_message("Никак нет: самого себя банить нельзя.", ephemeral=True)
            return

        try:
            await участник.ban(reason=причина)
            await interaction.response.send_message(f"Так точно: {участник.mention} забанен. Причина: {причина}")
        except discord.Forbidden:
            await interaction.response.send_message("Никак нет: у бота не хватает прав для бана.", ephemeral=True)
        except discord.HTTPException as error:
            await interaction.response.send_message(f"Докладываю: бан не выполнен: {error}", ephemeral=True)

    @app_commands.command(name="kick", description="Кикнуть участника")
    @app_commands.describe(участник="Кого кикнуть", причина="Причина кика")
    @app_commands.checks.has_permissions(kick_members=True)
    @app_commands.checks.bot_has_permissions(kick_members=True)
    async def kick(
        self,
        interaction: discord.Interaction,
        участник: discord.Member,
        причина: str = "Причина не указана",
    ) -> None:
        if участник == interaction.user:
            await interaction.response.send_message("Никак нет: самого себя кикать нельзя.", ephemeral=True)
            return

        try:
            await участник.kick(reason=причина)
            await interaction.response.send_message(f"Так точно: {участник.mention} кикнут. Причина: {причина}")
        except discord.Forbidden:
            await interaction.response.send_message("Никак нет: у бота не хватает прав для кика.", ephemeral=True)
        except discord.HTTPException as error:
            await interaction.response.send_message(f"Докладываю: кик не выполнен: {error}", ephemeral=True)

    @app_commands.command(name="clear", description="Очистить сообщения")
    @app_commands.describe(количество="Сколько сообщений удалить, по умолчанию 10")
    @app_commands.checks.has_permissions(manage_messages=True)
    @app_commands.checks.bot_has_permissions(manage_messages=True)
    async def clear(self, interaction: discord.Interaction, количество: int = 10) -> None:
        if not isinstance(interaction.channel, discord.TextChannel):
            await interaction.response.send_message("Никак нет: очистка доступна только в текстовом канале.", ephemeral=True)
            return

        amount = max(1, min(количество, 100))
        await interaction.response.defer(ephemeral=True)

        try:
            deleted = await interaction.channel.purge(limit=amount)
            await interaction.followup.send(f"Докладываю: удалено сообщений: {len(deleted)}.", ephemeral=True)
        except discord.Forbidden:
            await interaction.followup.send("Никак нет: у бота нет прав на удаление сообщений.", ephemeral=True)
        except discord.HTTPException as error:
            await interaction.followup.send(f"Докладываю: очистка не выполнена: {error}", ephemeral=True)

    @app_commands.command(name="slowmode", description="Установить медленный режим")
    @app_commands.describe(секунды="Задержка между сообщениями в секундах")
    @app_commands.checks.has_permissions(manage_channels=True)
    @app_commands.checks.bot_has_permissions(manage_channels=True)
    async def slowmode(self, interaction: discord.Interaction, секунды: int) -> None:
        if not isinstance(interaction.channel, discord.TextChannel):
            await interaction.response.send_message("Никак нет: slowmode доступен только в текстовом канале.", ephemeral=True)
            return

        delay = max(0, min(секунды, 21600))
        try:
            await interaction.channel.edit(slowmode_delay=delay)
            await interaction.response.send_message(f"Так точно: slowmode установлен на {delay} сек.")
        except discord.Forbidden:
            await interaction.response.send_message("Никак нет: у бота нет прав менять канал.", ephemeral=True)
        except discord.HTTPException as error:
            await interaction.response.send_message(f"Докладываю: slowmode не установлен: {error}", ephemeral=True)

    @app_commands.command(name="warn", description="Выдать предупреждение участнику")
    @app_commands.describe(участник="Кому выдать предупреждение", причина="Причина предупреждения")
    @app_commands.checks.has_permissions(moderate_members=True)
    async def warn(
        self,
        interaction: discord.Interaction,
        участник: discord.Member,
        причина: str = "Причина не указана",
    ) -> None:
        add_warn(участник.id, interaction.user.id, причина)
        await interaction.response.send_message(f"Так точно: {участник.mention} получил предупреждение. Причина: {причина}")

    @app_commands.command(name="warnings", description="Показать предупреждения участника")
    @app_commands.describe(участник="Чьи предупреждения показать")
    @app_commands.checks.has_permissions(moderate_members=True)
    async def warnings(self, interaction: discord.Interaction, участник: discord.Member) -> None:
        warns = get_warnings(участник.id)
        if not warns:
            await interaction.response.send_message(f"Докладываю: у {участник.mention} предупреждений нет.")
            return

        lines = []
        for warn in warns[:10]:
            date = dt.datetime.fromtimestamp(warn["created_at"], tz=dt.UTC).strftime("%d.%m.%Y %H:%M UTC")
            lines.append(f"#{warn['id']} — {date} — <@{warn['moderator_id']}>: {warn['reason']}")

        await interaction.response.send_message(
            f"Предупреждения {участник.mention} ({len(warns)}):\n" + "\n".join(lines)
        )

    async def cog_app_command_error(
        self,
        interaction: discord.Interaction,
        error: app_commands.AppCommandError,
    ) -> None:
        if isinstance(error, app_commands.MissingPermissions):
            message = "Никак нет: у тебя нет прав на эту команду."
        elif isinstance(error, app_commands.BotMissingPermissions):
            message = "Никак нет: у бота нет нужных прав."
        else:
            message = f"Докладываю: команда дала сбой: {error}"

        if interaction.response.is_done():
            await interaction.followup.send(message, ephemeral=True)
        else:
            await interaction.response.send_message(message, ephemeral=True)


async def setup(bot: commands.Bot) -> None:
    await bot.add_cog(Moderation(bot))
