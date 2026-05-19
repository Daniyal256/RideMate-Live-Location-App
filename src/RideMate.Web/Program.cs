using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.ResponseCompression;
using System.Text;

using RideMate.Infrastructure.Data;
using RideMate.Infrastructure.Hubs;
using RideMate.Infrastructure.Services;
using RideMate.Application.Services;
using RideMate.Domain.Entities;
using RideMate.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
        .ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = true;
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.User.RequireUniqueEmail = true;

        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddTransient<IEmailSender, EmailService>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();

builder.Services.AddSignalR();

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes =
        ResponseCompressionDefaults.MimeTypes
        .Concat(new[]
        {
            "application/octet-stream"
        });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseStaticFiles();

app.UseResponseCompression();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/", () => Results.Redirect("/login"));
app.MapGet("/auth/login", () => Results.Redirect("/login"));
app.MapGet("/auth/register", () => Results.Redirect("/register"));
app.MapGet("/auth/logout", () => Results.Redirect("/login"));

app.MapPost("/auth/register", async (
    HttpContext http,
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    AppDbContext db) =>
{
    var form = await http.Request.ReadFormAsync();

    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var displayName = form["displayName"].ToString();
    var avatar = form.Files["avatar"];
    string? avatarUrl = null;

    var user = new ApplicationUser
    {
        UserName = email,
        Email = email,
        DisplayName = displayName
    };

    if (string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(password) ||
        string.IsNullOrWhiteSpace(displayName))
    {
        return Results.Redirect("/register?error=missing-fields");
    }

    if (avatar is { Length: > 0 })
    {
        var allowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif"
        };

        if (!allowedTypes.TryGetValue(avatar.ContentType, out var extension))
        {
            return Results.Redirect("/register?error=Profile%20photo%20must%20be%20an%20image.");
        }

        if (avatar.Length > 4 * 1024 * 1024)
        {
            return Results.Redirect("/register?error=Profile%20photo%20must%20be%20under%204MB.");
        }

        var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsPath, fileName);

        await using var stream = File.Create(filePath);
        await avatar.CopyToAsync(stream);

        avatarUrl = $"/uploads/{fileName}";
    }

    user.AvatarUrl = avatarUrl;

    var result = await userManager.CreateAsync(user, password);

    if (result.Succeeded)
    {
        var inviteService = new InviteService();
        string inviteCode;

        do
        {
            inviteCode = inviteService.GenerateInviteCode();
        }
        while (await db.Circles.AnyAsync(c => c.InviteCode == inviteCode));

        var defaultCircle = new Circle
        {
            Name = "Family",
            InviteCode = inviteCode,
            CreatorId = user.Id
        };

        defaultCircle.Members.Add(new CircleMember
        {
            Circle = defaultCircle,
            UserId = user.Id
        });

        db.Circles.Add(defaultCircle);
        await db.SaveChangesAsync();

        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var confirmationLink =
            $"{http.Request.Scheme}://{http.Request.Host}/auth/confirm-email" +
            $"?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(encodedCode)}";

        await emailSender.SendEmailAsync(
            email,
            "Confirm your RideMate account",
            $"""
            <p>Welcome to RideMate, {displayName}.</p>
            <p>Confirm your email address to activate your account:</p>
            <p><a href="{confirmationLink}">Confirm email</a></p>
            """);

        return Results.Redirect("/login?registered=true&verifyEmail=true");
    }

    DeleteUploadedAvatarFile(app.Environment.WebRootPath, avatarUrl);

    var error = Uri.EscapeDataString(
        result.Errors.FirstOrDefault()?.Description ?? "Registration failed.");

    return Results.Redirect($"/register?error={error}");
});

app.MapPost("/auth/login", async (
    HttpContext http,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) =>
{
    var form = await http.Request.ReadFormAsync();

    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var user = await userManager.FindByEmailAsync(email);

    if (user == null)
    {
        return Results.Redirect("/login?error=invalid-login");
    }

    if (!await userManager.IsEmailConfirmedAsync(user))
    {
        return Results.Redirect("/login?error=email-not-confirmed");
    }

    var result = await signInManager.CheckPasswordSignInAsync(
        user,
        password,
        lockoutOnFailure: false);

    if (!result.Succeeded)
    {
        return Results.Redirect("/login?error=invalid-login");
    }

    await signInManager.SignInAsync(user, isPersistent: true);

    return string.IsNullOrWhiteSpace(returnUrl) ||
           !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
        ? Results.Redirect("/map/default")
        : Results.Redirect(returnUrl);
});

app.MapGet("/auth/confirm-email", async (
    string userId,
    string code,
    UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.FindByIdAsync(userId);

    if (user is null)
    {
        return Results.Redirect("/login?error=invalid-confirmation");
    }

    var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
    var result = await userManager.ConfirmEmailAsync(user, decodedCode);

    return result.Succeeded
        ? Results.Redirect("/login?emailConfirmed=true")
        : Results.Redirect("/login?error=invalid-confirmation");
});

app.MapPost("/auth/logout", async (
    SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();

    return Results.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<LocationHub>("/locationHub");

app.Run();

static void DeleteUploadedAvatarFile(string webRootPath, string? avatarUrl)
{
    if (string.IsNullOrWhiteSpace(avatarUrl) ||
        !avatarUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    var fileName = Path.GetFileName(avatarUrl);

    if (string.IsNullOrWhiteSpace(fileName))
    {
        return;
    }

    var path = Path.Combine(webRootPath, "uploads", fileName);

    if (File.Exists(path))
    {
        File.Delete(path);
    }
}
