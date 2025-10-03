using System;
using System.Collections.Generic;

namespace TicketSystem.Models
{
    // Admin Dashboard için ViewModel
    public class AdminDashboardVm
    {
        // Üst kartlar
        public int TotalTickets { get; set; }
        public int OpenCount { get; set; }
        public int ClosedCount { get; set; }

        // Hafta navigasyonu (0=bu hafta, -1=geçen hafta, +1=gelecek hafta)
        public int WeekOffset { get; set; }

        // Görsel metinler için
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }

        // Haftalık çubuk grafik verileri
        public List<string> WeekLabels { get; set; } = new(); // Mon..Sun gibi
        public int[] WeekLow { get; set; }    = new int[7];
        public int[] WeekMedium { get; set; } = new int[7];
        public int[] WeekHigh { get; set; }   = new int[7];
    }
}
