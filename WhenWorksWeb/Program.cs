using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Data;
using WhenWorksWeb.Data.Seed;
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

// Register the EventCodeGenerator as a scoped service, so it can be injected into controllers and other services.
builder.Services.AddScoped<UniqueCodeGenerator>();
// Register the DevelopmentDataSeeder as a scoped service, so it can be injected and used during application startup.
builder.Services.AddScoped<DevelopmentDataSeeder>();

builder.Services.AddControllersWithViews();

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
