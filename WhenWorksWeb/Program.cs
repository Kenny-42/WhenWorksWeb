using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Data;
using WhenWorksWeb.Data.Seed;
using WhenWorksWeb.Hubs;
using WhenWorksWeb.Models;
using WhenWorksWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(IdentityConfiguration.Configure)
    .AddRoles<IdentityRole>() // Add role support to Identity
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add Google as an external login provider, alongside the local username/password flow above.
// Client ID/secret come from configuration (dotnet user-secrets locally, environment variables/host
// secret store in production) and are intentionally not present in appsettings.json.
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]
            ?? throw new InvalidOperationException("Configuration value 'Authentication:Google:ClientId' not found.");
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
            ?? throw new InvalidOperationException("Configuration value 'Authentication:Google:ClientSecret' not found.");
    });

// Register the UniqueCodeGenerator as a scoped service, so it can be injected into controllers and other services.
builder.Services.AddScoped<UniqueCodeGenerator>();
// Register the DevelopmentDataSeeder as a scoped service, so it can be injected and used during application startup.
builder.Services.AddScoped<DevelopmentDataSeeder>();
// Register the EventDateCleanupService as a scoped service, so both EventsController and
// MyEventsController can remove now-empty candidate dates after an availability mark is removed.
builder.Services.AddScoped<EventDateCleanupService>();

builder.Services.AddControllersWithViews();

// Ships with ASP.NET Core's shared framework (no extra package) — powers the live-sync
// EventHub (see Hubs/EventHub.cs) that pushes availability/final-date changes to every
// connected viewer of an event's Home/Finalize page.
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.MapHub<EventHub>("/hubs/event");

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await IdentityRoleSeeder.SeedRolesAsync(roleManager);

    // Seed development data only in the development environment to avoid polluting production databases with test data.
    if (app.Environment.IsDevelopment())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
        await seeder.SeedAsync();
    }
}

app.Run();
