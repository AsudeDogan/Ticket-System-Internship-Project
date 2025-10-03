using System;

namespace TicketSystem.Models
{
    public class TicketAttachment
    {
        public int Id { get; set; }

        public int TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        // Kullanıcının gördüğü orijinal ad
        public string FileName { get; set; } = "";

        // Sunucuda benzersiz kaydedilen ad (GUID + uzantı)
        public string StoredFileName { get; set; } = "";

        // wwwroot içindeki göreli yol: /uploads/tickets/{ticketId}/{StoredFileName}
        public string FilePath { get; set; } = "";

        public string ContentType { get; set; } = "";
        public long Size { get; set; }

        public DateTime UploadedAt { get; set; }

        public string UploadedById { get; set; } = null!;
        public ApplicationUser? UploadedBy { get; set; }
    }
}
