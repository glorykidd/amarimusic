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
using System.Threading.RateLimiting;

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

// Turnstile CAPTCHA verification (contact form + admin login)
builder.Services.AddHttpClient(nameof(TurnstileService), client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddScoped<TurnstileService>();

// Rate limiting, partitioned per client IP so one abusive IP can't exhaust
// a shared bucket and lock out every other client.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", httpContext => PerIpFixedWindow(httpContext, permitLimit: 5, window: TimeSpan.FromMinutes(15)));
    options.AddPolicy("contact", httpContext => PerIpFixedWindow(httpContext, permitLimit: 5, window: TimeSpan.FromMinutes(15)));
});

// A null RemoteIpAddress shouldn't happen under normal IIS/Kestrel TCP
// hosting, but if it does (e.g. a misconfigured reverse proxy), fall back to
// a single shared "unknown" bucket so all such requests are throttled
// together. A fresh GUID per request would give each one its own unlimited
// bucket — defeating rate limiting entirely and leaking memory unboundedly.
static RateLimitPartition<string> PerIpFixedWindow(HttpContext httpContext, int permitLimit, TimeSpan window)
{
    var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window
        });
}

// Warn if email not configured
if (string.IsNullOrWhiteSpace(builder.Configuration["Email:SmtpHost"]))
{
    Console.WriteLine("WARNING: Email:SmtpHost not configured. Admin notifications will be skipped.");
}

// Warn if Turnstile not configured outside Development
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["Turnstile:SecretKey"]))
{
    Console.WriteLine("WARNING: Turnstile:SecretKey not configured. Contact form and admin login will run without CAPTCHA protection.");
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

// Security headers. CSP allows Bootstrap Icons (jsdelivr), Cloudflare Turnstile
// (script + its iframe challenge), and the Google Calendar iframe embed on
// /calendar; style-src allows 'unsafe-inline' for the many inline style="..."
// attributes already in the markup — a much smaller risk than allowing inline
// scripts, which stays disallowed.
app.Use((ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "frame-ancestors 'none'; " +
        "script-src 'self' https://challenges.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "font-src 'self' https://cdn.jsdelivr.net; " +
        "img-src 'self' data:; " +
        "frame-src https://challenges.cloudflare.com https://calendar.google.com; " +
        "connect-src 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    return next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

// Admin login POST handler
app.MapPost("/admin/do-login", async (HttpContext ctx, IConfiguration config, IAntiforgery antiforgery, TurnstileService turnstile) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(ctx);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    var form = await ctx.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    if (turnstile.IsConfigured)
    {
        var captchaToken = form["cf-turnstile-response"].ToString();
        var remoteIp = ctx.Connection.RemoteIpAddress?.ToString();
        if (!await turnstile.VerifyAsync(captchaToken, remoteIp))
            return Results.Redirect("/admin/login?_captchaError=true");
    }

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
app.MapPost("/admin/logout", async (HttpContext ctx, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(ctx);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();
