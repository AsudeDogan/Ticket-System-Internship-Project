using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Authorization;
using TicketSystem.Models;

namespace TicketSystem.Data
{
    public static class IdentitySeed
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 1) Rolleri oluştur
            await EnsureRoleExists(roleMgr, AppRoles.Admin);
            await EnsureRoleExists(roleMgr, AppRoles.Developer);
            await EnsureRoleExists(roleMgr, AppRoles.User);

            // 2) Varsayılan kullanıcılar
            var admin = await EnsureUserExists(userMgr,
                email: "admin1@gmail.com",
                password: "Admin123!",
                displayName: "Admin One");

            var dev = await EnsureUserExists(userMgr,
                email: "dev1@tickets.local",
                password: "Dev123!",
                displayName: "Dev One");

            var user = await EnsureUserExists(userMgr,
                email: "user1@gmail.com",
                password: "User123!",
                displayName: "User One");

            // 3) Rollerine ata
            await EnsureUserInRole(userMgr, admin, AppRoles.Admin);
            await EnsureUserInRole(userMgr, dev, AppRoles.Developer);
            await EnsureUserInRole(userMgr, user, AppRoles.User);

            // 4) İsteğe bağlı örnek veri
            if (!await db.Projects.AnyAsync())
            {
                db.Projects.Add(new Project { Name = "Default Project", Description = "Seeded for testing" });
                await db.SaveChangesAsync();
            }
        }

        private static async Task EnsureRoleExists(RoleManager<IdentityRole> roleMgr, string role)
        {
            if (!await roleMgr.RoleExistsAsync(role))
                _ = await roleMgr.CreateAsync(new IdentityRole(role));
        }

        private static async Task<ApplicationUser> EnsureUserExists(
            UserManager<ApplicationUser> userMgr, string email, string password, string displayName)
        {
            var user = await userMgr.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };
                var result = await userMgr.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    // Minimum şifre/ayar hatalarında basit bir mesaj bırak.
                    throw new InvalidOperationException("Seed user could not be created: " +
                        string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}")));
                }
            }

            return user;
        }

        private static async Task EnsureUserInRole(UserManager<ApplicationUser> userMgr, ApplicationUser user, string role)
        {
            if (!await userMgr.IsInRoleAsync(user, role))
                _ = await userMgr.AddToRoleAsync(user, role);
        }
    }
}
