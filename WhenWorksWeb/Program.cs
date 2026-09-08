using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WhenWorksWeb.Areas.Admin;
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
    .AddEntityFrameworkStores<ApplicationDbContext>()
    // Replaces the stock SignInManager with ApplicationSignInManager, which reorders
    // CheckPasswordSignInAsync to verify the password before consulting lockout/confirmation status
    // (see ApplicationSignInManager and Spec/Refactors/REFACTOR-signin-manager-and-confirmation-email.ospec).
    .AddSignInManager<ApplicationSignInManager>();

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

        // Surfaces Google's own "did we actually verify this address" answer as an "email_verified"
        // claim on info.Principal -- ASP.NET Core's Google handler maps "email" but not this one by
        // default. ExternalLogin.cshtml.cs's OnPostConfirmationAsync requires it before trusting an
        // account's email as pre-confirmed; see
        // Spec/Bugs/BUGS-external-login-email-verification-claim.ospec.
        options.ClaimActions.MapJsonKey("email_verified", "email_verified", ClaimValueTypes.Boolean);
    });

// Sends real confirmation/reset/change-email mail via Brevo's REST API (see BrevoEmailSender and
// Spec/Features/FEATURES-email-verification.ospec) instead of Identity's default no-op sender.
// Read and validated here, at top-level startup code, rather than inside the configureClient
// delegate below -- AddHttpClient<T>'s configureClient only runs lazily, on the first HttpClient
// creation for that typed client (e.g. the first registration in production), not during
// application startup, so a missing/blank key would otherwise start up successfully and only fail
// on that first real request. Reading it here also means it's read and validated once, not
// re-read/re-mutated into DefaultRequestHeaders on every HttpClient handed out for this typed
// client. See Spec/Refactors/REFACTOR-email-verification-review-cleanup.ospec.
var brevoApiKey = builder.Configuration["Brevo:ApiKey"];
if (string.IsNullOrWhiteSpace(brevoApiKey))
{
    throw new InvalidOperationException("Configuration value 'Brevo:ApiKey' not found.");
}

// AddHttpClient<T> gives BrevoEmailSender a typed HttpClient with a bounded request timeout (so a
// slow/hanging Brevo call can't tie up a request thread indefinitely) and the api-key auth header
// pre-attached -- the key is read from configuration (user-secrets locally, an environment variable
// in production) and never committed to appsettings.json, matching the Google OAuth secrets above.
builder.Services.AddHttpClient<BrevoEmailSender>(client =>
{
    client.BaseAddress = new Uri("https://api.brevo.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("api-key", brevoApiKey);
});
// Fetches the already-configured BrevoEmailSender registered above rather than a plain
// AddTransient<IEmailSender, BrevoEmailSender>() mapping -- that would construct a second
// BrevoEmailSender via ordinary constructor injection, which resolves its HttpClient parameter
// from ASP.NET Core's default (unconfigured) client rather than the typed client above, leaving
// it with no BaseAddress or api-key header.
builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<BrevoEmailSender>());

// Register the UniqueCodeGenerator as a scoped service, so it can be injected into controllers and other services.
builder.Services.AddScoped<UniqueCodeGenerator>();
// Register the DevelopmentDataSeeder as a scoped service, so it can be injected and used during application startup.
builder.Services.AddScoped<DevelopmentDataSeeder>();
// Register the EventDateCleanupService as a scoped service, so both EventsController and
// MyEventsController can remove now-empty candidate dates after an availability mark is removed.
builder.Services.AddScoped<EventDateCleanupService>();
// Registers the confirmation-email-sending sequence (token generation, callback URL, send) shared
// by Register.cshtml.cs, ResendEmailConfirmation.cshtml.cs, and ExternalLogin.cshtml.cs -- see
// EmailConfirmationLinkSender and Spec/Refactors/REFACTOR-signin-manager-and-confirmation-email.ospec.
builder.Services.AddScoped<EmailConfirmationLinkSender>();
// Registered as a singleton (unlike the scoped services above) since it holds no per-request
// state -- see its own remarks for why that's safe.
builder.Services.AddSingleton<TimeZoneOptionsProvider>();

builder.Services.AddControllersWithViews();

// Requires every Admin-role account to have 2FA enabled before it can use any Areas/Admin page --
// applied once here via a folder convention (see RequireTwoFactorPageFilter's own remarks) rather
// than on each Admin page individually, so it automatically covers future ones too.
builder.Services.AddRazorPages(options =>
    options.Conventions.AddAreaFolderApplicationModelConvention("Admin", "/", model =>
        model.Filters.Add(new RequireTwoFactorPageFilterFactory())));

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
