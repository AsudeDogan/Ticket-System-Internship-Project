using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Authorization;
using TicketSystem.Data;
using TicketSystem.Models;

namespace TicketSystem.Controllers
{
    [Authorize(Policy = TicketPolicies.CanView)]
    public class ProjectsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProjectsController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Projects
        public async Task<IActionResult> Index()
        {
            if (User.IsInRole(AppRoles.Admin))
            {
                var listAll = await _context.Projects
                    .AsNoTracking()
                    .OrderBy(p => p.Name)
                    .Select(p => new ProjectListVm
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        TicketCount = p.Tickets!.Count()
                    })
                    .ToListAsync();

                return View(listAll);
            }
            else
            {
                var uid = _userManager.GetUserId(User)!;
                bool isDev = User.IsInRole(AppRoles.Developer);

                var listFiltered = await _context.Projects
                    .AsNoTracking()
                    .OrderBy(p => p.Name)
                    .Select(p => new ProjectListVm
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        TicketCount = p.Tickets!.Count(t =>
                            isDev
                                ? (t.AssignedToId == uid || t.CreatedById == uid)
                                : (t.CreatedById == uid))
                    })
                    .Where(vm => vm.TicketCount > 0)
                    .ToListAsync();

                return View(listFiltered);
            }
        }

        // GET: /Projects/Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id is null) return NotFound();

            var project = await _context.Projects
               
                .Include(p => p.Tickets!)      .ThenInclude(t => t.CreatedBy)
                .Include(p => p.Tickets!)      .ThenInclude(t => t.AssignedTo)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project is null) return NotFound();

            // (Tickets null ise boş liste kullan)
            var tickets = project.Tickets?.AsEnumerable() ?? Enumerable.Empty<Ticket>();

            if (!User.IsInRole(AppRoles.Admin))
            {
                var uid = _userManager.GetUserId(User)!;

                if (User.IsInRole(AppRoles.Developer))
                {
                    tickets = tickets.Where(t => t.AssignedToId == uid || t.CreatedById == uid);
                }
                else 
                {
                    tickets = tickets.Where(t => t.CreatedById == uid);
                }
            }

            project.Tickets = tickets
                .OrderByDescending(t => t.CreatedAt)
                .ToList(); 

            return View(project);
        }

        // GET: /Projects/Create
        [Authorize(Policy = TicketPolicies.CanModify)]
        public IActionResult Create() => View();

        // POST: /Projects/Create
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = TicketPolicies.CanModify)]
        public async Task<IActionResult> Create([Bind("Name,Description")] Project project)
        {
            if (!ModelState.IsValid) return View(project);

            project.CreatedAt = DateTime.UtcNow;
            _context.Add(project);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Projects/Edit
        [Authorize(Policy = TicketPolicies.CanModify)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null) return NotFound();
            var project = await _context.Projects.FindAsync(id);
            if (project is null) return NotFound();
            return View(project);
        }

        // POST: /Projects/Edit
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = TicketPolicies.CanModify)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description")] Project project)
        {
            if (id != project.Id) return NotFound();
            if (!ModelState.IsValid) return View(project);

            var existing = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (existing is null) return NotFound();

            project.CreatedAt = existing.CreatedAt;

            _context.Update(project);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Projects/Delete
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id is null) return NotFound();

            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project is null) return NotFound();
            return View(project);
        }

        // POST: /Projects/Delete
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project is not null)
            {
                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }

    public class ProjectListVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int TicketCount { get; set; }
    }
}
