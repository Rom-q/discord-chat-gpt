# Discord bot that can communicate with user(s) using GPT 3.5

### Can:

  - enter correspondence and remember past messages

  - answer 1 request

  - you can easily add a prompt 

  - you can change the chat gpt settings

Needs improvement/rework

Currently not working due to chat gpt and discord ban in Russia

info command:

```c#
string infoMessage = "Привет! Я бот, созданный для общения с ChatGPT.\n";
            infoMessage += "Вот список доступных команд:\n";
            infoMessage += "- `/startchat`: Начать чат с ботом.\n";
            infoMessage += "- `/endchat`: Завершить текущий чат с ботом.\n";
            infoMessage += "- `/queue`: Получить память бота.\n";
            infoMessage += "- `/ask [сообщение]`: Получить ответ.\n";
            infoMessage += "- `/info`: Получить информацию о боте и его командах.\n";
            infoMessage += "- `mesask`: Это команда приложения, она делает тоже что и /ask но с выбранным сообщением.\n";
            infoMessage += "-  `/settings`: В ней можно настроить бота для различных задач.\n";
            infoMessage += "\nПриятного общения!";
```

# Bye
