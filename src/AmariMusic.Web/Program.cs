using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AmariMusic.Components;
using AmariMusic.Data;
using AmariMusic.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

// One-off CLI helper: `dotnet run -- hash-password` prompts for a password and
// prints a hash to paste into AdminAuth:PasswordHash, without needing a scripting
// tool. The password is read from stdin, not argv, so it never appears in the
// process list or shell history.
if (args.Length == 1 && args[0] == "hash-password")
{
    Console.Write("Enter password to hash: ");
    var password = Console.ReadLine() ?? string.Empty;
    Console.WriteLine(PasswordHasher.Hash(password));
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// Database
builder.Services.AddDbContext<ContactDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ContactDb") ?? "Data Source=data/contact.db"));

// Auth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();

// Email
builder.Services.AddScoped<EmailService>();

// Rate limiting on login endpoint
builder.Services.AddRateLimiter(options =>
    options.AddFixedWindowLimiter("login", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(15);
    }));

// Warn if email not configured
if (string.IsNullOrWhiteSpace(builder.Configuration["Email:SmtpHost"]))
{
    Console.WriteLine("WARNING: Email:SmtpHost not configured. Admin notifications will be skipped.");
}

// Refuse to boot outside Development with missing admin credentials
AdminAuthValidator.ValidateOrThrow(builder.Environment, builder.Configuration);

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContactDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

// Admin login POST handler
app.MapPost("/admin/do-login", async (HttpContext ctx, IConfiguration config, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(ctx);

    var form = await ctx.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    var expectedUser = config["AdminAuth:Username"] ?? string.Empty;
    var expectedPasswordHash = config["AdminAuth:PasswordHash"];

    var userMatch = CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(username), Encoding.UTF8.GetBytes(expectedUser));
    var passMatch = PasswordHasher.Verify(password, expectedPasswordHash);

    if (!userMatch || !passMatch)
        return Results.Redirect("/admin/login?_error=true");

    var claims = new[] { new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, "Admin") };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Redirect("/admin");
}).RequireRateLimiting("login");

// Admin logout
app.MapGet("/admin/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();
