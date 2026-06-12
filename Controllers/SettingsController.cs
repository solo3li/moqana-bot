using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TelegramAdminPanel.Data;
using TelegramAdminPanel.Models;

namespace TelegramAdminPanel.Controllers;

[Authorize]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public SettingsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var token = await _dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == "BotToken");
        var welcomeMessage = await _dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == "WelcomeMessage");

        ViewBag.BotToken = token?.Value ?? "";
        ViewBag.WelcomeMessage = welcomeMessage?.Value ?? "Welcome! This is the default start message.";

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string botToken, string welcomeMessage)
    {
        var tokenSetting = await _dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == "BotToken");
        if (tokenSetting == null)
        {
            _dbContext.BotSettings.Add(new BotSetting { Key = "BotToken", Value = botToken ?? "" });
        }
        else
        {
            tokenSetting.Value = botToken ?? "";
        }

        var welcomeSetting = await _dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == "WelcomeMessage");
        if (welcomeSetting == null)
        {
            _dbContext.BotSettings.Add(new BotSetting { Key = "WelcomeMessage", Value = welcomeMessage ?? "" });
        }
        else
        {
            welcomeSetting.Value = welcomeMessage ?? "";
        }

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Settings saved successfully! Changes might take a few seconds to apply.";

        return RedirectToAction(nameof(Index));
    }
}
