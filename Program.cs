using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore;   
using Microsoft.AspNetCore.Mvc;                               // AddDatabaseDeveloperPageExceptionFilter()

using TicketSystem.Authorization;   // AppRoles, TicketPolicies
using TicketSystem.Data;            // AppDbContext
using TicketSystem.Models;          // ApplicationUser

var builder = WebApplication.CreateBuilder(args);

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(opt =>
    {
        opt.SignIn.RequireConfirmedAccount = false;
        opt.Password.RequiredLength = 6;
        opt.Password.RequireNonAlphanumeric = false;
        opt.Password.RequireUppercase = false;
        opt.Password.RequireLowercase = false;
        opt.Password.RequireDigit = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath        = "/Identity/Account/Login";
    opt.LogoutPath       = "/Identity/Account/Logout";
    opt.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(TicketPolicies.CanView, policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole(AppRoles.Admin) ||
            ctx.User.IsInRole(AppRoles.Developer) ||
            ctx.User.IsInRole(AppRoles.User)));

    options.AddPolicy(TicketPolicies.CanModify, policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole(AppRoles.Admin) ||
            ctx.User.IsInRole(AppRoles.Developer)));

    options.AddPolicy(TicketPolicies.CanClose, policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole(AppRoles.Admin) ||
            ctx.User.IsInRole(AppRoles.Developer)));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // From Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Identity UI (Register/Login pages)
app.MapRazorPages();

// Seed roles & demo users
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await TicketSystem.Authorization.IdentitySeed.RunAsync(services);
}

await app.RunAsync();
