using GharAagan.Data;
using GharAagan.Models;
using GharAagan.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Database — SQLite by default (zero-setup, portable).
// To switch to SQL Server later, this is the ONE line to change:
//     options.UseSqlServer(connectionString)
// plus the "DefaultConnection" string in appsettings.json.
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=gharaagan.db";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// ---------------------------------------------------------------------------
// Identity with roles (Customer / Provider / Admin).
// Relaxed password rules for a demo/research project; tighten in production.
// ---------------------------------------------------------------------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Application services
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Seed database (roles, admin, categories, sample providers + reviews).
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

// Behind Render/most PaaS the TLS proxy forwards X-Forwarded-Proto;
// honor it so UseHttpsRedirection doesn't loop and auth cookies stay secure.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
