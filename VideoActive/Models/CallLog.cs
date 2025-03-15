using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoActive.Models
{
    public class CallLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CID { get; set; }

        [Required]
        public int CallerId { get; set; }

        [Required]
        public int CalleeId { get; set; }

        [Required]
        public DateTime CallTime { get; set; } = DateTime.UtcNow;

        public DateTime? EndTime { get; set; }

        public string? CallType { get; set; }
    }
}