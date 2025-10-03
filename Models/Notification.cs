using System;

namespace TicketSystem.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public string UserId { get; set; } = default!;
        public ApplicationUser? User { get; set; }

        public string Message { get; set; } = default!;

        public int? TicketId { get; set; }
        public Ticket? Ticket { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }
    }
}
