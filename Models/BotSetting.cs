using System;
using System.ComponentModel.DataAnnotations;

namespace TelegramAdminPanel.Models
{
    public class BotSetting
    {
        [Key]
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
