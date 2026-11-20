using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.API.Data;
using TaskFlow.API.Services;
using TaskFlow.API.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=taskflow.db";
if (builder.Environment.IsProduction())
{
    var sqliteConnection = new SqliteConnectionStringBuilder(connectionString);
    if (!Path.IsPathRooted(sqliteConnection.DataSource))
    {
        throw new InvalidOperationException(
            "Production requires an absolute SQLite database path on persistent storage.");
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwtSettings.Key))
{
    throw new InvalidOperationException("JWT signing key is missing. Set 'JwtSettings:Key' through User Secrets or an environment variable.");
}
if (builder.Environment.IsProduction() && Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32)
{
    throw new InvalidOperationException("Production requires a JWT signing key at least 32 bytes long.");
}

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddScoped<AuthService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
allowedOrigins = allowedOrigins
    .Select(origin => origin.TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
if (builder.Environment.IsProduction()
    && (allowedOrigins.Length == 0
        || allowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))))
{
    throw new InvalidOperationException(
        "Production requires Cors:AllowedOrigins to contain HTTPS origins without paths, queries or fragments.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("TaskFlowClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "Authorization");
    });
});

var app = builder.Build();
var httpsTerminatedAtProxy = builder.Configuration.GetValue<bool>("Hosting:HttpsTerminatedAtProxy");

if (app.Environment.IsEnvironment("Testing")
    || builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database;
    if (database.IsRelational())
    {
        await database.MigrateAsync();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    if (httpsTerminatedAtProxy)
    {
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.StrictTransportSecurity = "max-age=31536000";
                return Task.CompletedTask;
            });

            await next();
        });
    }
    else
    {
        app.UseHsts();
    }
}

app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!httpsTerminatedAtProxy)
{
    app.UseHttpsRedirection();
}
app.UseCors("TaskFlowClient");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    application = "TaskFlow API"
}));

app.MapGet("/api/health/live", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/api/health/ready", async (
    ApplicationDbContext context,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        var canConnect = await context.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Results.Ok(new { status = "Ready" })
            : Results.Json(new { status = "NotReady" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
    {
        logger.LogWarning(exception, "Database readiness check failed.");
        return Results.Json(new { status = "NotReady" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllers();

app.Run();

public partial class Program { }
