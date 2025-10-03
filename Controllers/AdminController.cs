using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Authorization;
using TicketSystem.Data;
using TicketSystem.Models;

namespace TicketSystem.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        public AdminController(AppDbContext context) => _context = context;

        // GET: /Admin/Dashboard?weekOffset=0
        public async Task<IActionResult> Dashboard(int weekOffset = 0)
        {
         
            var total  = await _context.Tickets.CountAsync();
            var open   = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.Open);
            var closed = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.Closed);

            // Haftanın başlangıcı: Pazartesi 
            var today = DateTime.UtcNow.Date;
            int diffToMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var weekStart = today.AddDays(-diffToMonday).AddDays(weekOffset * 7);
            var nextWeekStart = weekStart.AddDays(7);
            var weekEnd = weekStart.AddDays(6);

            var raw = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.CreatedAt >= weekStart && t.CreatedAt < nextWeekStart)
                .Select(t => new { t.CreatedAt, t.Priority })
                .ToListAsync();

            int[] low  = new int[7];
            int[] med  = new int[7];
            int[] high = new int[7];

            foreach (var r in raw)
            {
                int dayIndex = (int)(r.CreatedAt.Date - weekStart).TotalDays;
                if (dayIndex < 0 || dayIndex > 6) continue;

                switch (r.Priority)
                {
                    case TicketPriority.Low:    low[dayIndex]++;  break;
                    case TicketPriority.Medium: med[dayIndex]++;  break;
                    case TicketPriority.High:   high[dayIndex]++; break;
                }
            }

            var culture = CultureInfo.CurrentCulture;
            var labels = Enumerable.Range(0, 7)
                .Select(i => weekStart.AddDays(i).ToString("ddd", culture))
                .ToList();

            var vm = new AdminDashboardVm
            {
                TotalTickets = total,
                OpenCount = open,
                ClosedCount = closed,
                WeekOffset = weekOffset,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                WeekLabels = labels,
                WeekLow = low,
                WeekMedium = med,
                WeekHigh = high
            };

            return View(vm);
        }
    }
}
