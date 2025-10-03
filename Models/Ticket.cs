using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TicketSystem.Models
{
    public class Ticket
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        public TicketPriority Priority { get; set; }
        public TicketType     Type     { get; set; }
        public TicketStatus   Status   { get; set; } = TicketStatus.Open;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string CreatedById { get; set; } = default!;
        public ApplicationUser? CreatedBy { get; set; }

        public string? AssignedToId { get; set; }
        public ApplicationUser? AssignedTo { get; set; }

        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        // Yorumlar
        public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();

        // EKLENDİ: Dosya ekleri
        public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    }

    public enum TicketPriority { Low, Medium, High }
    public enum TicketType { Bug, Request, Question }
    public enum TicketStatus { Open, Closed }
}
