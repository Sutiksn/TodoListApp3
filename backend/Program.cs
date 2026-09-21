using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TodoListApp2.Data;
using TodoListApp2.Services;

// Enable Npgsql legacy timestamp behavior for universal DateTime compatibility
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1. Database Context (Entity Framework Core: PostgreSQL Only)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration.GetConnectionString("PostgreSqlConnection") 
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=localhost;Port=5432;Database=taskflow;Username=postgres;Password=postgres;SSL Mode=Prefer;";

// Auto-parse postgresql:// URI format if provided (Supabase, Neon, Render, Railway, AWS RDS, etc.)
if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || 
    connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var sslMode = queryParams["sslmode"] ?? (host == "localhost" || host == "127.0.0.1" ? "Prefer" : "Require");

        connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode={sslMode};Include Error Detail=true";
    }
    catch
    {
        // Keep original string if URI parsing fails
    }
}

// Register Npgsql PostgreSQL DbContext exclusively
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});
Console.WriteLine($"[Database] Connected exclusively to PostgreSQL: {MaskPassword(connectionString)}");

// 2. Business Services DI Registration
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAccessRequestService, AccessRequestService>();

// 3. JWT Authentication Configuration
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "SuperSecretEnterpriseKeyForTaskFlowPlatformMustBeAtLeast32BytesLong!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TaskFlowEnterprise";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TaskFlowUsers";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };
});

// 4. CORS Policy for decoupled frontend-backend communication
const string CorsPolicyName = "TaskFlowCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

// 5. Database Migration & Data Seeding at Startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        Console.WriteLine("[Database] Applying EF Core migrations...");
        db.Database.Migrate();
        Console.WriteLine("[Database] Migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[Database Migration Note] {ex.Message}");
        Console.ResetColor();
        db.Database.EnsureCreated();
    }
    DatabaseSeeder.Seed(db, app.Configuration);
}

// 6. Middleware Pipeline
// Disable caching on localhost/development to ensure instant delivery of newly edited UI
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate, max-age=0");
    context.Response.Headers.Append("Pragma", "no-cache");
    context.Response.Headers.Append("Expires", "0");
    await next();
});

// Configure frontend directory serving (standalone or unified mode)
var frontendPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "frontend"));
if (!Directory.Exists(frontendPath))
{
    var altPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "frontend"));
    if (Directory.Exists(altPath))
    {
        frontendPath = altPath;
    }
}
if (Directory.Exists(frontendPath))
{
    var fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate, max-age=0");
            ctx.Context.Response.Headers.Append("Pragma", "no-cache");
            ctx.Context.Response.Headers.Append("Expires", "0");
        }
    });
}
else
{
    app.UseStaticFiles();
}

app.UseRouting();

// CORS middleware MUST be between UseRouting and UseAuthentication/UseAuthorization
app.UseCors(CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/login", permanent: false));

if (Directory.Exists(frontendPath))
{
    var indexPath = Path.Combine(frontendPath, "index.html");
    app.MapFallback(async context =>
    {
        if (File.Exists(indexPath))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(indexPath);
        }
        else
        {
            context.Response.StatusCode = 404;
        }
    });
}
else
{
    app.MapFallbackToFile("index.html");
}

app.Run();

static string MaskPassword(string connStr)
{
    return System.Text.RegularExpressions.Regex.Replace(connStr, @"Password=[^;]+", "Password=******", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
