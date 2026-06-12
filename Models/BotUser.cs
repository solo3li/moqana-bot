using System;
using System.ComponentModel.DataAnnotations;

namespace TelegramAdminPanel.Models
{
    public class BotUser
    {
        [Key]
        public long ChatId { get; set; }
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
