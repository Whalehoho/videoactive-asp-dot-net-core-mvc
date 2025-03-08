using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoActive.Models
{
    public class Message
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MID { get; set; }

        [Required]
        public int SenderId { get; set; }
        [ForeignKey("SenderId")]
        public User Sender { get; set; }

        [Required]
        public int ReceiverId { get; set; }
        [ForeignKey("ReceiverId")]
        public User Receiver { get; set; }

        [Required]
        public int CID { get; set; }
        [ForeignKey("CID")]
        public Chatbox Chatbox { get; set; }

        public string? MessageText { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
