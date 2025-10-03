using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;          // IWebHostEnvironment
using Microsoft.AspNetCore.Http;             // IFormFile
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Authorization;
using TicketSystem.Data;
using TicketSystem.Models;

namespace TicketSystem.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Developer},{AppRoles.User}")]
    public class TicketsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        // Basit upload kuralları
        private static readonly string[] AllowedExt = new[] { ".png", ".jpg", ".jpeg", ".pdf", ".txt", ".log" };
        private const long MaxUpload = 10 * 1024 * 1024; // 10 MB

        public TicketsController(AppDbContext context,
                                 UserManager<ApplicationUser> userManager,
                                 IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // ========== INDEX ==========
        public async Task<IActionResult> Index(TicketPriority? priority, TicketType? type)
        {
            var userId = _userManager.GetUserId(User)!;

            IQueryable<Ticket> q = _context.Tickets
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo)
                .Include(t => t.Project);

            if (User.IsInRole(AppRoles.Admin))
            {
                // Admin her şeyi görür
            }
            else if (User.IsInRole(AppRoles.Developer))
            {
                q = q.Where(t => t.AssignedToId == userId || t.CreatedById == userId);
            }
            else
            {
                q = q.Where(t => t.CreatedById == userId);
            }

            if (priority.HasValue) q = q.Where(t => t.Priority == priority.Value);
            if (type.HasValue)     q = q.Where(t => t.Type == type.Value);

            var list = await q.OrderByDescending(t => t.CreatedAt).ToListAsync();

            ViewBag.PriorityList = new SelectList(
                Enum.GetValues(typeof(TicketPriority)).Cast<TicketPriority>()
                    .Select(p => new { Id = p, Name = p.ToString() }),
                "Id", "Name", priority
            );

            ViewBag.TypeList = new SelectList(
                Enum.GetValues(typeof(TicketType)).Cast<TicketType>()
                    .Select(tt => new { Id = tt, Name = tt.ToString() }),
                "Id", "Name", type
            );

            return View(list);
        }

        // ========== DETAILS ==========
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var ticket = await _context.Tickets
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo)
                .Include(t => t.Project)
                .Include(t => t.Comments).ThenInclude(c => c.CommentedBy)
                .Include(t => t.Attachments) // EKLENDİ: ekleri getir
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ticket == null) return NotFound();

            if (!User.IsInRole(AppRoles.Admin))
            {
                var userId = _userManager.GetUserId(User)!;
                var canSee =
                    (User.IsInRole(AppRoles.Developer) && (ticket.AssignedToId == userId || ticket.CreatedById == userId)) ||
                    (!User.IsInRole(AppRoles.Developer) && ticket.CreatedById == userId);

                if (!canSee) return Forbid();
            }

            return View(ticket);
        }

        // ========== YORUM EKLEME ==========
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int id, string commentText)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Comments)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            if (!User.IsInRole(AppRoles.Admin))
            {
                var userIdX = _userManager.GetUserId(User)!;
                var canSee =
                    (User.IsInRole(AppRoles.Developer) && (ticket.AssignedToId == userIdX || ticket.CreatedById == userIdX)) ||
                    (!User.IsInRole(AppRoles.Developer) && ticket.CreatedById == userIdX);

                if (!canSee) return Forbid();
            }

            if (string.IsNullOrWhiteSpace(commentText))
            {
                TempData["CommentError"] = "Comment cannot be empty.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var actorId = _userManager.GetUserId(User)!;

            var comment = new TicketComment
            {
                TicketId = id,
                CommentText = commentText.Trim(),
                CommentedAt = DateTime.UtcNow,
                CommentedById = actorId
            };

            _context.TicketComments.Add(comment);
            await _context.SaveChangesAsync();

            // Basit bildirimler
            if (!string.Equals(ticket.CreatedById, actorId, StringComparison.Ordinal))
            {
                await NotifyAsync(ticket.CreatedById, $"Your ticket \"{ticket.Title}\" received a new comment.", ticket.Id);
            }

            if (!string.IsNullOrEmpty(ticket.AssignedToId) &&
                !string.Equals(ticket.AssignedToId, actorId, StringComparison.Ordinal))
            {
                await NotifyAsync(ticket.AssignedToId, $"Ticket \"{ticket.Title}\" has a new comment.", ticket.Id);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ========== CREATE ==========
        public async Task<IActionResult> Create()
        {
            await LoadProjectsAsync();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Title,Description,Priority,Type,ProjectId")] Ticket ticket,
            List<IFormFile> attachments) // EKLENDİ: dosyalar
        {
            ticket.CreatedAt   = DateTime.UtcNow;
            ticket.Status      = TicketStatus.Open;
            ticket.CreatedById = _userManager.GetUserId(User)!;

            ModelState.Remove(nameof(Ticket.CreatedById));

            if (!await ProjectExistsOrNull(ticket.ProjectId))
                ModelState.AddModelError(nameof(Ticket.ProjectId), "Geçerli bir proje seçiniz.");

            // Basit yükleme kuralları
            foreach (var f in attachments ?? Enumerable.Empty<IFormFile>())
            {
                if (f?.Length > 0)
                {
                    var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
                    if (!AllowedExt.Contains(ext))
                        ModelState.AddModelError("attachments", $"İzin verilmeyen dosya türü: {ext}");
                    if (f.Length > MaxUpload)
                        ModelState.AddModelError("attachments", $"{f.FileName} 10MB limitini aşıyor.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadProjectsAsync(ticket.ProjectId);
                return View(ticket);
            }

            // 1) Önce ticket'ı kaydet (Id lazım)
            _context.Add(ticket);
            await _context.SaveChangesAsync();

            // 2) Dosyaları kaydet + kayıt oluştur
            if (attachments != null && attachments.Count > 0)
            {
                var userId = _userManager.GetUserId(User)!;
                var root = Path.Combine(_env.WebRootPath, "uploads", "tickets", ticket.Id.ToString());
                Directory.CreateDirectory(root);

                foreach (var file in attachments.Where(a => a?.Length > 0))
                {
                    var ext = Path.GetExtension(file.FileName);
                    var stored = $"{Guid.NewGuid()}{ext}";
                    var fullPath = Path.Combine(root, stored);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                        await file.CopyToAsync(stream);

                    var relPath = $"/uploads/tickets/{ticket.Id}/{stored}";

                    _context.TicketAttachments.Add(new TicketAttachment
                    {
                        TicketId = ticket.Id,
                        FileName = Path.GetFileName(file.FileName),
                        StoredFileName = stored,
                        FilePath = relPath,
                        ContentType = file.ContentType ?? "application/octet-stream",
                        Size = file.Length,
                        UploadedAt = DateTime.UtcNow,
                        UploadedById = userId
                    });
                }

                await _context.SaveChangesAsync();
            }

            // Yeni ticket bildirimi: tüm admin'lere (oluşturan admin hariç)
            var creatorId = ticket.CreatedById!;
            var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
            foreach (var admin in admins)
            {
                if (admin.Id != creatorId)
                {
                    await NotifyAsync(admin.Id, $"New ticket created: \"{ticket.Title}\"", ticket.Id);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // ========== EDIT ==========
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Developer}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var ticket = await _context.Tickets
                .Include(t => t.AssignedTo)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            if (User.IsInRole(AppRoles.Developer))
            {
                var userId = _userManager.GetUserId(User)!;
                if (ticket.AssignedToId != null && ticket.AssignedToId != userId)
                    return Forbid();
            }

            await LoadProjectsAsync(ticket.ProjectId);

            if (User.IsInRole(AppRoles.Admin))
            {
                var devs = await _userManager.GetUsersInRoleAsync(AppRoles.Developer);
                ViewData["AssignedToId"] = new SelectList(devs, "Id", "UserName", ticket.AssignedToId);
            }

            return View(ticket);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Developer}")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Priority,Type,Status,ProjectId,AssignedToId")] Ticket ticket)
        {
            if (id != ticket.Id) return NotFound();

            var existing = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (existing == null) return NotFound();

            if (User.IsInRole(AppRoles.Developer))
            {
                var userId = _userManager.GetUserId(User)!;
                if (existing.AssignedToId != null && existing.AssignedToId != userId)
                    return Forbid();

                // Dev atamayı değiştiremez
                ticket.AssignedToId = existing.AssignedToId;
            }

            ModelState.Remove(nameof(Ticket.CreatedById));

            if (!await ProjectExistsOrNull(ticket.ProjectId))
                ModelState.AddModelError(nameof(Ticket.ProjectId), "Geçerli bir proje seçiniz.");

            if (ModelState.IsValid)
            {
                try
                {
                    var assignedChanged = existing.AssignedToId != ticket.AssignedToId;

                    ticket.CreatedAt   = existing.CreatedAt;
                    ticket.CreatedById = existing.CreatedById;

                    _context.Update(ticket);
                    await _context.SaveChangesAsync();

                    // Admin geliştirici atadıysa bildir
                    if (User.IsInRole(AppRoles.Admin) && assignedChanged && !string.IsNullOrEmpty(ticket.AssignedToId))
                    {
                        await NotifyAsync(ticket.AssignedToId, $"A ticket \"{ticket.Title}\" has been assigned to you.", ticket.Id);
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await TicketExists(ticket.Id)) return NotFound();
                    throw;
                }
            }

            await LoadProjectsAsync(ticket.ProjectId);
            if (User.IsInRole(AppRoles.Admin))
            {
                var devs = await _userManager.GetUsersInRoleAsync(AppRoles.Developer);
                ViewData["AssignedToId"] = new SelectList(devs, "Id", "UserName", ticket.AssignedToId);
            }

            return View(ticket);
        }

        // ========== DELETE ==========
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var ticket = await _context.Tickets
                .Include(t => t.Project)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ticket == null) return NotFound();
            return View(ticket);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket != null)
            {
                _context.Tickets.Remove(ticket);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // ========== Hızlı Durum ==========
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Developer}")]
        public async Task<IActionResult> Close(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            if (User.IsInRole(AppRoles.Developer))
            {
                var userId = _userManager.GetUserId(User)!;
                if (ticket.AssignedToId != null && ticket.AssignedToId != userId)
                    return Forbid();
            }

            ticket.Status = TicketStatus.Closed;
            _context.Update(ticket);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Developer}")]
        public async Task<IActionResult> Open(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            if (User.IsInRole(AppRoles.Developer))
            {
                var userId = _userManager.GetUserId(User)!;
                if (ticket.AssignedToId != null && ticket.AssignedToId != userId)
                    return Forbid();
            }

            ticket.Status = TicketStatus.Open;
            _context.Update(ticket);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // ========== Helpers ==========
        private async Task<bool> TicketExists(int id) =>
            await _context.Tickets.AnyAsync(e => e.Id == id);

        private async Task LoadProjectsAsync(int? selectedId = null)
        {
            var projects = await _context.Projects
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            ViewData["ProjectId"] = new SelectList(projects, "Id", "Name", selectedId);
        }

        private async Task<bool> ProjectExistsOrNull(int? projectId)
        {
            if (projectId is null) return true;
            return await _context.Projects.AnyAsync(p => p.Id == projectId);
        }

        // Basit bildirim üretici
        private async Task NotifyAsync(string? userId, string message, int? ticketId = null)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;

            _context.Notifications.Add(new Notification
            {
                UserId = userId!,
                Message = message,
                TicketId = ticketId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });

            await _context.SaveChangesAsync();
        }
    }
}
