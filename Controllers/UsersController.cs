using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using TelegramAdminPanel.Data;
using TelegramAdminPanel.Models;

namespace TelegramAdminPanel.Controllers;

[Authorize]
public class UsersController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public UsersController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index(string? searchQuery)
    {
        var usersQuery = _dbContext.BotUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var searchLower = searchQuery.ToLower();
            usersQuery = usersQuery.Where(u => 
                (u.FirstName != null && u.FirstName.ToLower().Contains(searchLower)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(searchLower)) ||
                (u.Username != null && u.Username.ToLower().Contains(searchLower)) ||
                u.ChatId.ToString().Contains(searchLower));
        }

        var users = await usersQuery.OrderByDescending(u => u.JoinedAt).ToListAsync();
        ViewData["SearchQuery"] = searchQuery;
        return View(users);
    }

    public async Task<IActionResult> Chat(long id)
    {
        var user = await _dbContext.BotUsers.FirstOrDefaultAsync(u => u.ChatId == id);
        if (user == null) return NotFound();

        var messages = await _dbContext.BotMessages
            .Where(m => m.ChatId == id)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        ViewBag.User = user;
        return View(messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(long chatId, string text, IFormFile? mediaFile, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(text) && mediaFile == null)
        {
            TempData["ErrorMessage"] = "Message and media cannot both be empty.";
            if (!string.IsNullOrEmpty(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Chat), new { id = chatId });
        }

        var tokenSetting = await _dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == "BotToken");
        if (tokenSetting != null && !string.IsNullOrWhiteSpace(tokenSetting.Value))
        {
            try
            {
                var botClient = new TelegramBotClient(tokenSetting.Value);
                Telegram.Bot.Types.Message sentMessage;

                if (mediaFile != null && mediaFile.Length > 0)
                {
                    using var stream = mediaFile.OpenReadStream();
                    var inputFile = Telegram.Bot.Types.InputFile.FromStream(stream, mediaFile.FileName);
                    var contentType = mediaFile.ContentType.ToLower();

                    if (contentType.StartsWith("image/"))
                    {
                        sentMessage = await botClient.SendPhoto(chatId, inputFile, caption: text);
                    }
                    else if (contentType.StartsWith("video/"))
                    {
                        sentMessage = await botClient.SendVideo(chatId, inputFile, caption: text);
                    }
                    else if (contentType.StartsWith("audio/") || contentType == "audio/ogg")
                    {
                        // Some clients send audio as voice
                        sentMessage = await botClient.SendAudio(chatId, inputFile, caption: text);
                    }
                    else
                    {
                        sentMessage = await botClient.SendDocument(chatId, inputFile, caption: text);
                    }
                }
                else
                {
                    sentMessage = await botClient.SendMessage(chatId, text);
                }

                string dbText = sentMessage.Text ?? sentMessage.Caption ?? "";
                if (mediaFile != null)
                {
                    dbText = $"[Media: {mediaFile.FileName}] " + dbText;
                }

                var botReply = new BotMessage
                {
                    ChatId = chatId,
                    Text = string.IsNullOrWhiteSpace(dbText) ? "[Media Only]" : dbText,
                    Timestamp = DateTime.UtcNow,
                    IsFromBot = true
                };
                _dbContext.BotMessages.Add(botReply);
                await _dbContext.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Message sent successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to send message: {ex.Message}";
            }
        }
        else
        {
             TempData["ErrorMessage"] = "Bot token is not set. Go to settings.";
        }

        if (!string.IsNullOrEmpty(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Chat), new { id = chatId });
    }

    public IActionResult Broadcast()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendBroadcast(string text, IFormFile? mediaFile)
    {
        if (string.IsNullOrWhiteSpace(text) && mediaFile == null)
        {
            TempData["ErrorMessage"] = "Message and media cannot both be empty.";
            return RedirectToAction(nameof(Broadcast));
        }

        var tokenSetting = await _dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == "BotToken");
        if (tokenSetting == null || string.IsNullOrWhiteSpace(tokenSetting.Value))
        {
            TempData["ErrorMessage"] = "Bot token is not set. Go to settings.";
            return RedirectToAction(nameof(Broadcast));
        }

        var botClient = new TelegramBotClient(tokenSetting.Value);
        var users = await _dbContext.BotUsers.ToListAsync();
        int successCount = 0;

        string? mediaFileId = null;
        string mediaType = "";

        foreach (var user in users)
        {
            try
            {
                Telegram.Bot.Types.Message sentMessage;

                if (mediaFile != null && mediaFile.Length > 0)
                {
                    if (string.IsNullOrEmpty(mediaFileId))
                    {
                        // Upload for the first user
                        using var stream = mediaFile.OpenReadStream();
                        var inputFile = Telegram.Bot.Types.InputFile.FromStream(stream, mediaFile.FileName);
                        var contentType = mediaFile.ContentType.ToLower();

                        if (contentType.StartsWith("image/"))
                        {
                            sentMessage = await botClient.SendPhoto(user.ChatId, inputFile, caption: text);
                            mediaFileId = sentMessage.Photo?.LastOrDefault()?.FileId;
                            mediaType = "photo";
                        }
                        else if (contentType.StartsWith("video/"))
                        {
                            sentMessage = await botClient.SendVideo(user.ChatId, inputFile, caption: text);
                            mediaFileId = sentMessage.Video?.FileId;
                            mediaType = "video";
                        }
                        else if (contentType.StartsWith("audio/") || contentType == "audio/ogg")
                        {
                            sentMessage = await botClient.SendAudio(user.ChatId, inputFile, caption: text);
                            mediaFileId = sentMessage.Audio?.FileId ?? sentMessage.Voice?.FileId;
                            mediaType = "audio";
                        }
                        else
                        {
                            sentMessage = await botClient.SendDocument(user.ChatId, inputFile, caption: text);
                            mediaFileId = sentMessage.Document?.FileId;
                            mediaType = "document";
                        }
                    }
                    else
                    {
                        // Send to subsequent users using FileId
                        var inputFileId = Telegram.Bot.Types.InputFile.FromFileId(mediaFileId);
                        switch (mediaType)
                        {
                            case "photo":
                                sentMessage = await botClient.SendPhoto(user.ChatId, inputFileId, caption: text);
                                break;
                            case "video":
                                sentMessage = await botClient.SendVideo(user.ChatId, inputFileId, caption: text);
                                break;
                            case "audio":
                                sentMessage = await botClient.SendAudio(user.ChatId, inputFileId, caption: text);
                                break;
                            default:
                                sentMessage = await botClient.SendDocument(user.ChatId, inputFileId, caption: text);
                                break;
                        }
                    }
                }
                else
                {
                    sentMessage = await botClient.SendMessage(user.ChatId, text);
                }

                string dbText = sentMessage.Text ?? sentMessage.Caption ?? "";
                if (mediaFile != null)
                {
                    dbText = $"[Media: {mediaFile.FileName}] " + dbText;
                }

                var botReply = new BotMessage
                {
                    ChatId = user.ChatId,
                    Text = string.IsNullOrWhiteSpace(dbText) ? "[Media Only]" : dbText,
                    Timestamp = DateTime.UtcNow,
                    IsFromBot = true
                };
                _dbContext.BotMessages.Add(botReply);
                successCount++;
            }
            catch
            {
                // Ignore users who blocked the bot or errors for specific users
            }
        }

        await _dbContext.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Broadcast sent successfully to {successCount} out of {users.Count} users.";

        return RedirectToAction(nameof(Broadcast));
    }
}
