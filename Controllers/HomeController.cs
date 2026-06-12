using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TelegramAdminPanel.Data;
using TelegramAdminPanel.Models;

namespace TelegramAdminPanel.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public HomeController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var totalUsers = await _dbContext.BotUsers.CountAsync();
        var totalMessages = await _dbContext.BotMessages.CountAsync();

        ViewBag.TotalUsers = totalUsers;
        ViewBag.TotalMessages = totalMessages;

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
