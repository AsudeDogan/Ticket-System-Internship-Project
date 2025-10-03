
namespace TicketSystem.Authorization
{
    // Sadece policy adlarını tutuyoruz.
    public static class TicketPolicies
    {
        public const string CanView   = "CanViewTickets";
        public const string CanModify = "CanModifyTickets";
        public const string CanClose  = "CanCloseTickets";
    }
}
