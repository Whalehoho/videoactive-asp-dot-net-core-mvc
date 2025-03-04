using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoActive.Models
{
    public class Chatbox
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CID { get; set; }

        [Required]
        public int UserId1 { get; set; }
        [ForeignKey("UserId1")]
        public User User1 { get; set; }

        [Required]
        public int UserId2 { get; set; }
        [ForeignKey("UserId2")]
        public User User2 { get; set; }
    }
}
