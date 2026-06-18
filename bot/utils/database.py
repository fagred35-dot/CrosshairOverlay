import sqlite3
import time
from pathlib import Path
from typing import Any


# SQLite-файл лежит рядом с кодом бота, чтобы данные сохранялись между перезапусками.
DB_PATH = Path(__file__).resolve().parents[1] / "gray.sqlite3"


def _connect() -> sqlite3.Connection:
    connection = sqlite3.connect(DB_PATH)
    connection.row_factory = sqlite3.Row
    return connection


def init_db() -> None:
    with _connect() as connection:
        connection.execute(
            """
            CREATE TABLE IF NOT EXISTS users (
                user_id INTEGER PRIMARY KEY,
                balance INTEGER NOT NULL DEFAULT 0,
                last_daily INTEGER
            )
            """
        )
        connection.execute(
            """
            CREATE TABLE IF NOT EXISTS warns (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                moderator_id INTEGER NOT NULL,
                reason TEXT NOT NULL,
                created_at INTEGER NOT NULL
            )
            """
        )
        connection.execute(
            """
            CREATE TABLE IF NOT EXISTS twitch_channels (
                channel_name TEXT NOT NULL,
                discord_channel_id INTEGER NOT NULL,
                guild_id INTEGER NOT NULL,
                is_live INTEGER NOT NULL DEFAULT 0,
                last_stream_id TEXT,
                PRIMARY KEY (channel_name, guild_id)
            )
            """
        )


def add_warn(user_id: int, moderator_id: int, reason: str) -> None:
    with _connect() as connection:
        connection.execute(
            """
            INSERT INTO warns (user_id, moderator_id, reason, created_at)
            VALUES (?, ?, ?, ?)
            """,
            (user_id, moderator_id, reason, int(time.time())),
        )


def get_warnings(user_id: int) -> list[dict[str, Any]]:
    with _connect() as connection:
        rows = connection.execute(
            """
            SELECT id, user_id, moderator_id, reason, created_at
            FROM warns
            WHERE user_id = ?
            ORDER BY created_at DESC
            """,
            (user_id,),
        ).fetchall()

    return [dict(row) for row in rows]


def get_balance(user_id: int) -> int:
    with _connect() as connection:
        row = connection.execute(
            "SELECT balance FROM users WHERE user_id = ?",
            (user_id,),
        ).fetchone()

    return int(row["balance"]) if row else 0


def update_balance(user_id: int, amount: int) -> int:
    with _connect() as connection:
        connection.execute(
            """
            INSERT INTO users (user_id, balance)
            VALUES (?, ?)
            ON CONFLICT(user_id) DO UPDATE SET balance = balance + excluded.balance
            """,
            (user_id, amount),
        )
        row = connection.execute(
            "SELECT balance FROM users WHERE user_id = ?",
            (user_id,),
        ).fetchone()

    return int(row["balance"]) if row else 0


def get_last_daily(user_id: int) -> int | None:
    with _connect() as connection:
        row = connection.execute(
            "SELECT last_daily FROM users WHERE user_id = ?",
            (user_id,),
        ).fetchone()

    if row is None or row["last_daily"] is None:
        return None

    return int(row["last_daily"])


def set_last_daily(user_id: int, timestamp: int) -> None:
    with _connect() as connection:
        connection.execute(
            """
            INSERT INTO users (user_id, last_daily)
            VALUES (?, ?)
            ON CONFLICT(user_id) DO UPDATE SET last_daily = excluded.last_daily
            """,
            (user_id, timestamp),
        )


def add_twitch_channel(channel_name: str, discord_channel_id: int, guild_id: int) -> None:
    normalized_name = channel_name.lower().strip()
    with _connect() as connection:
        connection.execute(
            """
            INSERT INTO twitch_channels (channel_name, discord_channel_id, guild_id)
            VALUES (?, ?, ?)
            ON CONFLICT(channel_name, guild_id) DO UPDATE SET
                discord_channel_id = excluded.discord_channel_id
            """,
            (normalized_name, discord_channel_id, guild_id),
        )


def remove_twitch_channel(channel_name: str, guild_id: int) -> bool:
    normalized_name = channel_name.lower().strip()
    with _connect() as connection:
        cursor = connection.execute(
            """
            DELETE FROM twitch_channels
            WHERE channel_name = ? AND guild_id = ?
            """,
            (normalized_name, guild_id),
        )

    return cursor.rowcount > 0


def get_all_twitch_channels(guild_id: int | None = None) -> list[dict[str, Any]]:
    with _connect() as connection:
        if guild_id is None:
            rows = connection.execute(
                """
                SELECT channel_name, discord_channel_id, guild_id, is_live, last_stream_id
                FROM twitch_channels
                ORDER BY channel_name
                """
            ).fetchall()
        else:
            rows = connection.execute(
                """
                SELECT channel_name, discord_channel_id, guild_id, is_live, last_stream_id
                FROM twitch_channels
                WHERE guild_id = ?
                ORDER BY channel_name
                """,
                (guild_id,),
            ).fetchall()

    return [dict(row) for row in rows]


def update_twitch_status(
    channel_name: str,
    guild_id: int,
    is_live: bool,
    last_stream_id: str | None,
) -> None:
    normalized_name = channel_name.lower().strip()
    with _connect() as connection:
        connection.execute(
            """
            UPDATE twitch_channels
            SET is_live = ?, last_stream_id = ?
            WHERE channel_name = ? AND guild_id = ?
            """,
            (1 if is_live else 0, last_stream_id, normalized_name, guild_id),
        )
