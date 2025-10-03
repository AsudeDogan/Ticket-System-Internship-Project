using Microsoft.AspNetCore.Identity;

namespace TicketSystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; } 
    }
}
