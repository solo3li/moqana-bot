using System;
using System.ComponentModel.DataAnnotations;

namespace TelegramAdminPanel.Models
{
    public class BotMessage
    {
        [Key]
        public int Id { get; set; }
        public long ChatId { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsFromBot { get; set; }

        public BotUser? User { get; set; }
    }
}
