using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Channels;
using System.Threading.Tasks;
using ChatGPT.Net;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Discord.Rest;
using System.Globalization;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace SimpleBot
{
    public class Program
    {
        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;
        private ChatGpt _chatGpt;
        private bool _isChatting;
        private Queue<string> _messageHistory;
        private List<LogMessage> _logMessages;
        private Dictionary<ulong, List<string>> _userMessageHistory = new Dictionary<ulong, List<string>>();
        private ulong _chatChannelId;
        private bool _isredacted;
        private int errorcount;
        private Random rand = new Random();
        private bool islogged = true;

        public static void Main(string[] args)
            => new Program().RunBotAsync().GetAwaiter().GetResult();

        public async Task RunBotAsync()
        {
            string configPath = "config.json";
            string jsonString = File.ReadAllText(configPath);
            dynamic config = JsonConvert.DeserializeObject(jsonString);
            string gptToken = config.gpt_token;
            string botToken = config.bot_token;
            _client = new DiscordSocketClient();
            _commands = new CommandService();
            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .BuildServiceProvider();
            _chatGpt = new ChatGpt(gptToken, new ChatGPT.Net.DTO.ChatGPT.ChatGptOptions
            {
                MaxTokens = 2000,
                Model = "gpt-3.5-turbo",
                Temperature = 1,
                FrequencyPenalty= 0.2,
                PresencePenalty= 0.2
            });
            _isChatting = false;
            _messageHistory = new Queue<string>();
            _logMessages = new List<LogMessage>();
            _client.Log += Log;
            
            await RegisterCommandsAsync();
            int randNum = rand.Next(1, 3);
            if (randNum == 1)
            {
                await _client.SetActivityAsync(new Game("Топ 10 грустных сыров", ActivityType.Watching));
            }
            else if (randNum == 2)
            {
                await _client.SetActivityAsync(new Game("10 способов приготовить сыр", ActivityType.Watching));
            }
            else if (randNum == 3)
            {
                await _client.SetActivityAsync(new Game("Готовка сыра 10 часов", ActivityType.Streaming));
            }
            await _client.LoginAsync(TokenType.Bot, botToken);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        private Task Log(LogMessage arg)
        {
            Console.WriteLine(arg);
            _logMessages.Add(arg);
            return Task.CompletedTask;
        }

        public async Task RegisterCommandsAsync()
        {
            _client.MessageReceived += HandleMessageAsync;
            _client.MessageCommandExecuted += MessageCommandHandler;

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            _client.Ready += async () =>
            {
                Console.WriteLine("Бот подключен!");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var startChatCommand = new SlashCommandBuilder()
                            .WithName("startchat")
                            .WithDescription("Начать чат с ChatGPT");
                        await _client.Rest.CreateGlobalCommand(startChatCommand.Build());
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Ошибка при создании команды 'startchat': {exception.Message}");
                    }

                    try
                    {
                        var endChatCommand = new SlashCommandBuilder()
                            .WithName("endchat")
                            .WithDescription("Завершить текущий чат с ChatGPT");
                        await _client.Rest.CreateGlobalCommand(endChatCommand.Build());
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Ошибка при создании команды 'endchat': {exception.Message}");
                    }

                    try
                    {
                        var queueCommand = new SlashCommandBuilder()
                            .WithName("queue")
                            .WithDescription("Получить все, что знает бот");
                        await _client.Rest.CreateGlobalCommand(queueCommand.Build());
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Ошибка при создании команды 'queue': {exception.Message}");
                    }

                    try
                    {
                        var askCommand = new SlashCommandBuilder()
                            .WithName("ask")
                            .WithDescription("Ответ на вопрос")
                            .AddOption("message", ApplicationCommandOptionType.String, "Ваше сообщение");

                        await _client.Rest.CreateGlobalCommand(askCommand.Build());
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Ошибка при создании команды 'ask': {exception.Message}");
                    }

                    try
                    {
                        var infoCommand = new SlashCommandBuilder()
                                .WithName("info")
                                .WithDescription("Получить информацию о боте и его командах");
                        await _client.Rest.CreateGlobalCommand(infoCommand.Build());
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Ошибка при создании команды 'info': {exception.Message}");
                    }

                    try
                    {
                        var settingscommand = new SlashCommandBuilder()
                            .WithName("settings")
                            .WithDescription("Настройки бота")
                            .AddOption("temperature", ApplicationCommandOptionType.String, "Это метод, используемый для того чтобы ответ был более интересным или консервативным")
                            .AddOption("presence", ApplicationCommandOptionType.String, "Это метод, используемый для обеспечения того, чтобы GPT не использовал повторяющиеся фразы или идеи")
                            .AddOption("frequency", ApplicationCommandOptionType.String, "Это метод, ограничивает вывод языковой модели менее распространенными фразами или словами")
                            .AddOption("reset", ApplicationCommandOptionType.String, "Напиши reset для сброса настроек, в ином случае покажет какие сейчас настройки");

                        await _client.Rest.CreateGlobalCommand(settingscommand.Build());
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Ошибка при создании команды 'settings': {exception.Message}");
                    }

                    try
                    {
                        var mesask = new MessageCommandBuilder()
                            .WithName("mesask");
                        await _client.Rest.CreateGlobalCommand(mesask.Build());
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Ошибка при создании команды 'mesask': {exception.Message}");
                    }
                });
                await Task.CompletedTask;
            };

            _client.InteractionCreated += async interaction =>
            {
                if (interaction is SocketSlashCommand slashCommand)
                {
                    await SlashCommandHandler(slashCommand);
                }
            };
        }

        private async Task HandleMessageAsync(SocketMessage arg)
        {
            if (!(_chatChannelId == arg.Channel.Id))
            {
                return;
            }
            var message = arg as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);
            if (message.Author.IsBot)
            {
                return;
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    
                    if (_isChatting && message.Channel is SocketGuildChannel guildChannel)
                    {
                        if ((guildChannel.Name != "logs" && !_userMessageHistory.ContainsKey(message.Author.Id)))
                        {
                            if (context.Message.Attachments.Count > 0)
                            {
                                var attachments = context.Message.Attachments;
                                WebClient myWebClient = new WebClient();
                                string file = attachments.ElementAt(0).Filename;
                                string url = attachments.ElementAt(0).Url;
                                byte[] buffer = myWebClient.DownloadData(url);
                                string download = Encoding.UTF8.GetString(buffer);
                                _messageHistory.Enqueue(message.Content + download);
                            }
                            else
                            {
                                _messageHistory.Enqueue(message.Content);
                            }
                            
                            string chatInput = string.Join("\n", _messageHistory);
                            var i = await context.Channel.SendMessageAsync("Получаем ответ...");
                            string chatOutput = await _chatGpt.Ask(chatInput);
                            await context.Channel.DeleteMessageAsync(i);
                            _messageHistory.Enqueue(chatOutput);
                            if (chatOutput.Length > 2000)
                            {
                                if (islogged == true)
                                {
                                    Console.WriteLine($"Пользователь {message.Author}: {message.Content}" + "\n" + $"Бот: {chatOutput}");
                                }
                                var responseFilePath = Path.Combine(Path.GetTempPath(), "response.txt");
                                File.WriteAllText(responseFilePath, chatOutput);
                                await context.Channel.SendFileAsync(responseFilePath, Format.Bold($"Пользователь {message.Author}:") + $" {message.Content}" + "\n" + Format.Bold("Бот:"));
                                errorcount = 0;
                            }
                            else
                            {
                                if (islogged == true)
                                {
                                    Console.WriteLine($"Пользователь {message.Author}: {message.Content}" + "\n" + $"Бот: {chatOutput}");
                                }
                                
                                await context.Channel.SendMessageAsync(Format.Bold($"Пользователь {message.Author}:") + $" {message.Content}" + "\n" + Format.Bold("Бот:") + $" {chatOutput}");
                                errorcount = 0;
                            }
                            if (_messageHistory.Count > 20)
                            {
                                _messageHistory.Dequeue();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (islogged == true)
                    {
                        Console.WriteLine($"Ошибка при обработке сообщения: {ex.Message}");
                    }
                    errorcount += 1;
                    await context.Channel.SendMessageAsync("Ошибка при обработке сообщения " + errorcount);
                    if (errorcount > 4)
                    {
                        
                        //int randNum = rand.Next(1, 3);
                        //if (randNum == 1)
                        //{
                        //    await context.Channel.SendMessageAsync("Похоже кто то задумался о смысле жизни");
                        //}
                        //else if (randNum == 2)
                        //{
                        //    await context.Channel.SendMessageAsync("Похоже кто то будет сегодня уволен");
                        //}
                        //else if (randNum == 3)
                        //{
                        //    await context.Channel.SendMessageAsync("Похоже кто то уснул в новогоднем салате");
                        //}
                        if (_messageHistory.Count > 0)
                        {
                            _messageHistory.Dequeue();
                            await context.Channel.SendMessageAsync(Format.Code("Бот: Я освободил очередь 1 ступень теперь должно стать лучше"));
                        }
                        else
                        {
                            await context.Channel.SendMessageAsync(Format.Code("Бот: Похоже мои мозги " + Format.Bold("отсутсвуют, ") + "оставте меня в покое на некоторое время или напишите запрос поменьше"));
                        }
                    }
                }
            });

            await Task.CompletedTask;
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            var channel = command.Channel;
            if (channel == null)
            {
                return;
            }
            switch (command.Data.Name)
            {
                case "startchat":
                    await StartChatCommand(command.Data, channel);
                    break;
                case "endchat":
                    await EndChatCommand(command.Data, channel);
                    break;
                case "queue":
                    await QueueCommand(command.Data, channel);
                    break;
                case "ask":
                    await askCommand(command.Data, channel);
                    break;
                case "info":
                    await InfoCommand(command.Data, channel);
                    break;
                case "settings":
                    await SettingsCommand(command.Data, channel);
                    break;

            }

            await command.RespondAsync($"Использована {command.Data.Name}");
            await command.DeleteOriginalResponseAsync();
        }
        public async Task MessageCommandHandler(SocketMessageCommand command)
        {
            var channel = command.Channel;
            if (channel == null)
            {
                return;
            }
            switch (command.Data.Name)
            {
                case "mesask":
                    await Mesask(command.Data, channel);
                    break;
            }

            await command.RespondAsync($"Использована {command.Data.Name}");
            await command.DeleteOriginalResponseAsync();
        }
        private async Task Mesask(SocketMessageCommandData commandData, ISocketMessageChannel channel)
        {
            var message = commandData.Message.Content;
            _ = Task.Run(async () =>
            {
                try
                {
                    if (islogged == true)
                    {
                        Console.WriteLine("В " + channel.Id + " использовали mesask");
                    }
                    //if (_isredacted == false)
                    //{
                    //    _chatGpt.Config.Temperature = 1.5;
                    //    _chatGpt.Config.FrequencyPenalty = 0.5;
                    //    _chatGpt.Config.PresencePenalty = 0.5;
                    //}
                    var i = await channel.SendMessageAsync("Получаем ответ...");
                    string chatOutput = await _chatGpt.Ask(message);
                    await channel.DeleteMessageAsync(i);
                    //if (_isredacted == false)
                    //{
                    //    _chatGpt.Config.Temperature = 1;
                    //    _chatGpt.Config.FrequencyPenalty = 0.2;
                    //    _chatGpt.Config.PresencePenalty = 0.2;
                    //}
                    if (islogged == true)
                    {
                        Console.WriteLine(Format.Bold("Ответ на сообщение ") + '"' + message + '"' + '-' + chatOutput);
                    }
                    var responseFilePath = Path.Combine(Path.GetTempPath(), "response.txt");
                            File.WriteAllText(responseFilePath, chatOutput);
                            await channel.SendFileAsync(responseFilePath, Format.Bold("Ответ на сообщение ") + '"' + message + '"');
                }
                catch (Exception exception)
                {
                    if (islogged == true)
                    {
                        Console.WriteLine($"Ошибка при обработке команды 'mesask': {exception.Message}");
                    }    
                    await channel.SendMessageAsync($"Ошибка при обработке команды 'mesask'");
                }
            });

            await Task.CompletedTask;
        }

        private async Task StartChatCommand(SocketSlashCommandData commandData, ISocketMessageChannel channel)
        {
            if (islogged == true)
            {
                Console.WriteLine("В " + channel.Id + " использовали startchat");
            }
            if (!_isChatting && channel.Id != 0)
            {
                _isChatting = true;
                await channel.SendMessageAsync("Чат начат");
                _chatChannelId = channel.Id; 
                if (islogged == true)
                {
                    Console.WriteLine("Чат начат в " + _chatChannelId);
                }
            }
            else if(_chatChannelId == channel.Id)
            {
                await channel.SendMessageAsync("Чат уже идет");
            }
            else
            {
                await channel.SendMessageAsync("Чат идет в другом канале");
            }
        }

        private async Task EndChatCommand(SocketSlashCommandData commandData, ISocketMessageChannel channel)
        {
            if (islogged == true)
            {
                Console.WriteLine("В " + channel.Id + " использовали endchat");
            }
            if (!(_chatChannelId == channel.Id) && _chatChannelId != 0)
            {
                await channel.SendMessageAsync("Чат идет в другом канале");
                return;
            }
            if (_isChatting)
            {
                _isChatting = false;
                await channel.SendMessageAsync("Чат окончен");
                _chatChannelId = 0;
                _messageHistory.Clear();
            }
            else
            {
                await channel.SendMessageAsync("Чат окончен. Напишите /startchat для начала");
            }
            errorcount = 0;
            //Console.WriteLine("Сохраненные логи:");
            //foreach (var logMessage in _logMessages)
            //{
            //    Console.WriteLine(logMessage);
            //}
        }

        private async Task QueueCommand(SocketSlashCommandData commandData, ISocketMessageChannel channel)
        {
            if (islogged == true)
            {
                Console.WriteLine("В " + channel.Id + " использовали куеуе");
            }
            if (!(_chatChannelId == channel.Id ) && _chatChannelId != 0)
            {
                await channel.SendMessageAsync("Чат идет в другом канале");
                return;
            }
            try
            {
                if (_messageHistory.Count > 0)
                {
                    string queueOutput = string.Join("\n", _messageHistory);
                    var responseFilePath = Path.Combine(Path.GetTempPath(), "response.txt");
                    File.WriteAllText(responseFilePath, queueOutput);

                    await channel.SendFileAsync(responseFilePath);
                }
                else await channel.SendMessageAsync("Очередь пуста");
            }
            catch (Exception exception)
            {
                if (islogged == true)
                {
                    Console.WriteLine($"Ошибка при обработке команды 'queue': {exception.Message}");
                }
                await channel.SendMessageAsync("Ошибка при обработке команды 'queue'");
            }
        }

        private async Task askCommand(SocketSlashCommandData commandData, ISocketMessageChannel channel)
        {
            _ = Task.Run(async () =>
            {
                try
            {
                var subCommand = commandData.Options.FirstOrDefault();
                if (subCommand != null)
                {
                    var messageOption = subCommand.Value as string;
                    if (!string.IsNullOrEmpty(messageOption))
                    {
                            if (islogged == true)
                            {
                                Console.WriteLine("В " + channel.Id + " использовали ask");
                            }
                            //if (_isredacted == false)
                            //{
                            //    _chatGpt.Config.Temperature = 1.5;
                            //    _chatGpt.Config.FrequencyPenalty = 0.5;
                            //    _chatGpt.Config.PresencePenalty = 0.5;
                            //}
                            var i = await channel.SendMessageAsync("Получаем ответ...");
                            string chatOutput = await _chatGpt.Ask(messageOption);
                            await channel.DeleteMessageAsync(i);
                            //if (_isredacted == false)
                            //{
                            //    _chatGpt.Config.Temperature = 1;
                            //    _chatGpt.Config.FrequencyPenalty = 0.2;
                            //    _chatGpt.Config.PresencePenalty = 0.2;
                            //}
                            if (islogged == true)
                            {
                                Console.WriteLine(Format.Bold("Ответ на сообщение ") + '"' + messageOption + '"' + '-' + chatOutput);
                            }
                            var responseFilePath = Path.Combine(Path.GetTempPath(), "response.txt");
                        File.WriteAllText(responseFilePath, chatOutput);

                        await channel.SendFileAsync(responseFilePath, Format.Bold("Ответ на ") + '"' + messageOption + '"');
                    }
                    else
                    {
                        await channel.SendMessageAsync("Пожалуйста, укажите сообщение");
                    }
                }
                else
                {
                    await channel.SendMessageAsync("Неправильные подкоманды для команды 'ask'");
                }
            }
            catch (Exception exception)
            {
                    if(islogged == true)
                    {
                        Console.WriteLine($"Ошибка при обработке команды 'ask': {exception.Message}");
                    }
                await channel.SendMessageAsync("Ошибка при обработке команды 'ask'");
            }
            });

            await Task.CompletedTask;
        }
        private async Task InfoCommand(SocketSlashCommandData commandData, ISocketMessageChannel channel)
        {
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

            await channel.SendMessageAsync(infoMessage);
            if (islogged == true)
            {
                Console.Write("В " + channel.Id + " использовали info");
            }
        }
        private async Task SettingsCommand(SocketSlashCommandData command, ISocketMessageChannel channel)
        {
            if (islogged == true)
            {
                Console.WriteLine("В " + channel.Id + " использовали settings");
            }
            if (_isChatting == true)
            {
                await channel.SendMessageAsync("Бот занят в другом канале попробуйте позже");
                return;
            }
            var fieldName = command.Options.First().Name;
            var fieldValue = command.Options.First().Value;

            switch (fieldName)
            {
                case "temperature":
                    {
                        if (fieldValue is string strValue && (strValue.Contains('.') || strValue.Contains(',')) && double.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double temperature) && temperature >= 0.1 && temperature <= 1.5)
                        {
                            _chatGpt.Config.Temperature = temperature;
                            if (islogged == true)
                            {
                                Console.WriteLine($"Теперь у бота температура - {temperature}");
                            }
                            await channel.SendMessageAsync($"Теперь у бота температура - {temperature}");
                            _isredacted = true;
                        }
                        else
                        {
                            await channel.SendMessageAsync("temperature должен быть числом от 0.1 до 1.5 и разделятся точкой");
                        }
                    }
                    break;
                case "presence":
                    {
                        if (fieldValue is string strValue && (strValue.Contains('.') || strValue.Contains(',')) && double.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double presencePenalty) && presencePenalty >= 0 && presencePenalty <= 1)
                        {
                            _chatGpt.Config.PresencePenalty = presencePenalty;
                            if (islogged == true)
                            {
                                Console.WriteLine($"Теперь у бота ограничение присутствия - {presencePenalty}");
                            }
                            await channel.SendMessageAsync($"Теперь у бота ограничение присутствия - {presencePenalty}");
                            _isredacted = true;
                        }
                        else
                        {
                            await channel.SendMessageAsync("presence penalty должен быть числом от 0 до 1 и разделятся точкой");
                        }
                    }
                    break;
                case "frequency":
                    {
                        if (fieldValue is string strValue && (strValue.Contains('.') || strValue.Contains(',')) && double.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double frequencyPenalty) && frequencyPenalty >= 0 && frequencyPenalty <= 1)
                        {
                            _chatGpt.Config.FrequencyPenalty = frequencyPenalty;
                            if (islogged == true)
                            {
                                Console.WriteLine($"Теперь у бота ограничение частоты - {frequencyPenalty}");
                            }
                            await channel.SendMessageAsync($"Теперь у бота ограничение частоты - {frequencyPenalty}");
                            _isredacted= true;
                        }
                        else
                        {
                            await channel.SendMessageAsync("frequency penalty должен быть числом от 0 до 1 и разделятся точкой");
                        }
                    }
                    break;
                case "reset":
                    {
                        if (fieldValue is string strValue && strValue == "reset")
                        {
                            _chatGpt.Config.Temperature = 1;
                            _chatGpt.Config.FrequencyPenalty = 0.2;
                            _chatGpt.Config.PresencePenalty = 0.2;
                            if (islogged == true)
                            {
                                Console.WriteLine("Использована reset");
                            }
                        }
                        else
                        {
                            double temperature = _chatGpt.Config.Temperature;
                            double presencePenalty = _chatGpt.Config.PresencePenalty;
                            double frequencyPenalty = _chatGpt.Config.FrequencyPenalty;
                            _isredacted= false;
                            await channel.SendMessageAsync($"Текущие настройки бота: \nТемпература - {temperature} \nОграничение присутствия - {presencePenalty} \nОграничение частоты - {frequencyPenalty}");
                        }
                    }
                    break;
            }
        }


    }
}
