using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoActive.Models
{
    public class Relationship
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RID { get; set; }

        [Required]
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; }

        [Required]
        public int FriendId { get; set; }
        [ForeignKey("FriendId")]
        public User Friend { get; set; }

        [Required]
        public RelationshipStatus Status { get; set; } = RelationshipStatus.Pending;
    }

    public enum RelationshipStatus
    {
        Pending,
        Accepted
    }
}
