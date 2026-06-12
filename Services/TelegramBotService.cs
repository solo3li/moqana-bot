using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramAdminPanel.Data;
using TelegramAdminPanel.Models;

namespace TelegramAdminPanel.Services
{
    public class TelegramBotService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TelegramBotService> _logger;
        private TelegramBotClient? _botClient;
        private string? _currentBotToken;

        public TelegramBotService(IServiceProvider serviceProvider, ILogger<TelegramBotService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    
                    var tokenSetting = await dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == "BotToken", stoppingToken);
                    
                    if (tokenSetting != null && !string.IsNullOrWhiteSpace(tokenSetting.Value))
                    {
                        if (_botClient == null || _currentBotToken != tokenSetting.Value)
                        {
                            _currentBotToken = tokenSetting.Value;
                            _botClient = new TelegramBotClient(_currentBotToken);
                            _logger.LogInformation("Bot Client started with token.");
                            
                            var receiverOptions = new ReceiverOptions
                            {
                                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
                            };

                            _botClient.StartReceiving(
                                updateHandler: HandleUpdateAsync,
                                errorHandler: HandlePollingErrorAsync,
                                receiverOptions: receiverOptions,
                                cancellationToken: stoppingToken
                            );
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Bot token is not set. Admin must set the bot token from the UI.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Bot Service.");
                }

                // Check for token updates periodically
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is not { } message) return;
            if (message.Text is not { } messageText) return;

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var chatId = message.Chat.Id;

            // Ensure User exists
            var botUser = await dbContext.BotUsers.FirstOrDefaultAsync(u => u.ChatId == chatId, cancellationToken);
            if (botUser == null)
            {
                botUser = new BotUser
                {
                    ChatId = chatId,
                    Username = message.Chat.Username,
                    FirstName = message.Chat.FirstName,
                    LastName = message.Chat.LastName,
                    JoinedAt = DateTime.UtcNow
                };
                dbContext.BotUsers.Add(botUser);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Save Message
            var botMsg = new BotMessage
            {
                ChatId = chatId,
                Text = messageText,
                Timestamp = DateTime.UtcNow,
                IsFromBot = false
            };
            dbContext.BotMessages.Add(botMsg);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Handle Commands
            if (messageText.StartsWith("/start"))
            {
                var welcomeMessageSetting = await dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == "WelcomeMessage", cancellationToken);
                var welcomeMessage = welcomeMessageSetting?.Value;
                
                if (string.IsNullOrWhiteSpace(welcomeMessage))
                {
                    welcomeMessage = "Welcome! This is the default start message.";
                }

                var sentMessage = await botClient.SendMessage(
                    chatId: chatId,
                    text: welcomeMessage,
                    cancellationToken: cancellationToken);

                // Save Bot Reply
                var botReply = new BotMessage
                {
                    ChatId = chatId,
                    Text = sentMessage.Text ?? welcomeMessage,
                    Timestamp = DateTime.UtcNow,
                    IsFromBot = true
                };
                dbContext.BotMessages.Add(botReply);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Telegram Bot API Error");
            return Task.CompletedTask;
        }
    }
}
