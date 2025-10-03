using System.ComponentModel.DataAnnotations;

namespace TicketSystem.Models
{
    public class TicketComment
    {
        public int Id { get; set; }

        [Required]
        public int TicketId { get; set; }
        public Ticket? Ticket { get; set; }

        [Required, StringLength(4000)]
        public string CommentText { get; set; } = string.Empty;

        public DateTime CommentedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [Required]
        public string CommentedById { get; set; } = default!;
        public ApplicationUser? CommentedBy { get; set; }
    }
}
