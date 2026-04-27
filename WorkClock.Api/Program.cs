using Microsoft.EntityFrameworkCore;
using WorkClock.Api.Data;
using WorkClock.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── HttpClient for TimeService ────────────────────────────────────────────────
builder.Services.AddHttpClient("TimeApi", client =>
{
    client.BaseAddress = new Uri("https://timeapi.io/");
    client.Timeout     = TimeSpan.FromSeconds(10);
});

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<ITimeService, TimeService>();

// ── Web / API ─────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Allow the React dev server (port 5173) during development
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Auto-apply pending migrations on startup in dev so `dotnet ef database update` is optional.
    // Wrapped in try/catch so the API still starts (and Swagger works) even without SQL Server.
    try
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not apply database migrations. The API will start, but database-backed endpoints will fail until SQL Server is available.");
    }
}

app.UseCors("DevCors");
app.UseAuthorization();
app.MapControllers();

app.Run();
