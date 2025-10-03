using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TicketSystem.Authorization;
using TicketSystem.Models;

namespace TicketSystem.Authorization
{
    public static class IdentitySeed
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Rolleri garanti et
            await EnsureRoleExists(roleMgr, AppRoles.Admin);
            await EnsureRoleExists(roleMgr, AppRoles.Developer);
            await EnsureRoleExists(roleMgr, AppRoles.User);

           
        }

        private static async Task EnsureRoleExists(RoleManager<IdentityRole> roleMgr, string role)
        {
            if (!await roleMgr.RoleExistsAsync(role))
                await roleMgr.CreateAsync(new IdentityRole(role));
        }

        // kullanıcı yoksa oluşturur; rolde değilse role ekler
        private static async Task CreateUserIfNotExistsAsync(
            UserManager<ApplicationUser> userMgr,
            string email, string password, string role)
        {
            var user = await userMgr.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var create = await userMgr.CreateAsync(user, password);
                if (!create.Succeeded)
                    throw new Exception("User create failed: " +
                        string.Join("; ", create.Errors.Select(e => e.Description)));
            }

            if (!await userMgr.IsInRoleAsync(user, role))
                await userMgr.AddToRoleAsync(user, role);
        }

        // herhangi bir kullanıcıyı bir role eklemek için
        public static async Task EnsureUserInRoleAsync(IServiceProvider services, string email, string role)
        {
            using var scope = services.CreateScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            if (!await roleMgr.RoleExistsAsync(role))
                await roleMgr.CreateAsync(new IdentityRole(role));

            var user = await userMgr.FindByEmailAsync(email);
            if (user != null && !await userMgr.IsInRoleAsync(user, role))
                await userMgr.AddToRoleAsync(user, role);
        }
    }
}
