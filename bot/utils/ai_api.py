import os
from typing import Any

import aiohttp


OPENROUTER_URL = "https://openrouter.ai/api/v1/chat/completions"
DEFAULT_MODEL = "google/gemini-2.0-flash-001"

SYSTEM_PROMPT = (
    "Ты — бот-связист с позывным «Грай» на военном Discord-сервере. "
    "Твой чин — прапорщик. Отвечай чётко, по-военному кратко, но с долей "
    "армейского юмора. Используй обращения: «Так точно», «Никак нет», "
    "«Докладываю», «Разрешите обратиться», «Здравия желаю», «Вольно». "
    "Можешь вставлять армейские байки и фразочки. Если не знаешь ответа — "
    "честно докладывай: «Не могу знать, разрешите уточнить». Ты помогаешь "
    "бойцам с вопросами и развлечениями. Задача — поддерживать боевой дух сервера!"
)


async def ask_openrouter(
    history: list[dict[str, str]],
    model: str = DEFAULT_MODEL,
) -> str:
    api_key = os.getenv("OPENROUTER_API_KEY")
    if not api_key:
        return "OpenRouter API не настроен: отсутствует OPENROUTER_API_KEY."

    messages: list[dict[str, str]] = [{"role": "system", "content": SYSTEM_PROMPT}]
    messages.extend(history)

    payload: dict[str, Any] = {
        "model": model,
        "messages": messages,
        "temperature": 0.7,
        "max_tokens": 700,
    }
    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
        "HTTP-Referer": "https://discloudbot.com",
        "X-Title": "Gray Discord Bot",
    }

    try:
        timeout = aiohttp.ClientTimeout(total=45)
        async with aiohttp.ClientSession(timeout=timeout) as session:
            async with session.post(OPENROUTER_URL, json=payload, headers=headers) as response:
                data = await response.json(content_type=None)

                if response.status >= 400:
                    detail = data.get("error", data)
                    return f"Докладываю: OpenRouter вернул ошибку {response.status}: {detail}"

                choices = data.get("choices", [])
                if not choices:
                    return "Не могу знать, разрешите уточнить: OpenRouter не вернул ответ."

                content = choices[0].get("message", {}).get("content")
                if not content:
                    return "Не могу знать, разрешите уточнить: ответ OpenRouter пуст."

                return str(content).strip()
    except aiohttp.ClientError as error:
        return f"Докладываю: связь с OpenRouter потеряна: {error}"
    except TimeoutError:
        return "Докладываю: OpenRouter слишком долго молчит."
    except Exception as error:
        return f"Докладываю: непредвиденная ошибка ИИ-связи: {error}"
