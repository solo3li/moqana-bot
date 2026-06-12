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
    public async Task<IActionResult> SendMessage(long chatId, string text, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["ErrorMessage"] = "Message cannot be empty.";
            if (!string.IsNullOrEmpty(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Chat), new { id = chatId });
        }

        var tokenSetting = await _dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == "BotToken");
        if (tokenSetting != null && !string.IsNullOrWhiteSpace(tokenSetting.Value))
        {
            try
            {
                var botClient = new TelegramBotClient(tokenSetting.Value);
                var sentMessage = await botClient.SendMessage(chatId, text);

                var botReply = new BotMessage
                {
                    ChatId = chatId,
                    Text = sentMessage.Text ?? text,
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
    public async Task<IActionResult> SendBroadcast(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["ErrorMessage"] = "Message cannot be empty.";
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

        foreach (var user in users)
        {
            try
            {
                var sentMessage = await botClient.SendMessage(user.ChatId, text);
                var botReply = new BotMessage
                {
                    ChatId = user.ChatId,
                    Text = sentMessage.Text ?? text,
                    Timestamp = DateTime.UtcNow,
                    IsFromBot = true
                };
                _dbContext.BotMessages.Add(botReply);
                successCount++;
            }
            catch
            {
                // Ignore users who blocked the bot
            }
        }

        await _dbContext.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Broadcast sent successfully to {successCount} out of {users.Count} users.";

        return RedirectToAction(nameof(Broadcast));
    }
}
