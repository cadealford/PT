using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using System.Collections;
using Microsoft.IdentityModel.Tokens;
using PetroTransit.API.Data;
using PetroTransit.API.Models;
using PetroTransit.API.Services;

LoadDotEnvFiles();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing configuration: ConnectionStrings:DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Persist data protection keys so password reset tokens survive app restarts
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

// Add Identity services
builder.Services.AddIdentity<AppUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Add Email Service
builder.Services.AddTransient<ISmtpSender, SmtpSender>();
builder.Services.AddTransient<IEmailService, EmailService>();

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtToken = builder.Configuration["AppSettings:Token"]
        ?? throw new InvalidOperationException("Missing configuration: AppSettings:Token");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtToken)),
        ValidateIssuer = false,
        ValidateAudience = false,
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAssertion(context =>
            string.Equals(context.User.FindFirst("isAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase)));
});

var allowedOrigins = GetAllowedOrigins(builder.Configuration);

// Configure CORS for the separate frontend subdomain.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Apply EF Core migrations automatically at startup (for today: simplest possible)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        await SeedDomainDataAsync(db);

        // Optional dev seed user, configured through local environment variables only.
        var seedEmail = app.Configuration["Seed:Email"];
        var seedUsername = app.Configuration["Seed:Username"];
        var seedPassword = app.Configuration["Seed:Password"];

        if (!string.IsNullOrWhiteSpace(seedEmail) &&
            !string.IsNullOrWhiteSpace(seedUsername) &&
            !string.IsNullOrWhiteSpace(seedPassword))
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var existingUser = await userManager.FindByEmailAsync(seedEmail);
            if (existingUser == null)
            {
                var seedUser = new AppUser
                {
                    UserName = seedUsername,
                    Email = seedEmail,
                    FullName = seedUsername,
                    IsAdmin = true
                };
                var createResult = await userManager.CreateAsync(seedUser, seedPassword);
                if (createResult.Succeeded)
                {
                    Console.WriteLine($"Seeded admin user: {seedEmail} (username: {seedUsername}, temp password set).");
                }
                else
                {
                    Console.WriteLine($"Failed to seed test user {seedEmail}: {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
                }
            }
            else if (!existingUser.IsAdmin)
            {
                existingUser.IsAdmin = true;
                var updateResult = await userManager.UpdateAsync(existingUser);
                if (updateResult.Succeeded)
                {
                    Console.WriteLine($"Updated seeded user {seedEmail} to admin.");
                }
                else
                {
                    Console.WriteLine($"Failed to update seeded user {seedEmail} to admin: {string.Join("; ", updateResult.Errors.Select(e => e.Description))}");
                }
            }
        }
    }
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); 
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Use CORS policy (CRITICAL for frontend communication)
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();

static void LoadDotEnvFiles()
{
    foreach (var envFilePath in GetDotEnvCandidatePaths())
    {
        if (!File.Exists(envFilePath))
        {
            continue;
        }

        foreach (DictionaryEntry entry in ParseDotEnvFile(envFilePath))
        {
            var key = entry.Key.ToString();
            if (string.IsNullOrWhiteSpace(key) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            Environment.SetEnvironmentVariable(key, entry.Value?.ToString());
        }
    }
}

static IEnumerable<string> GetDotEnvCandidatePaths()
{
    var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var root in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(root);
        while (directory is not null)
        {
            var envPath = Path.Combine(directory.FullName, ".env");
            if (seenPaths.Add(envPath))
            {
                yield return envPath;
            }

            directory = directory.Parent;
        }
    }
}

static IDictionary ParseDotEnvFile(string envFilePath)
{
    var values = new Hashtable(StringComparer.OrdinalIgnoreCase);

    foreach (var rawLine in File.ReadAllLines(envFilePath))
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();

        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            value = value[1..^1];
        }

        values[key] = value;
    }

    return values;
}

static string[] GetAllowedOrigins(IConfiguration configuration)
{
    var configuredOrigins = configuration["Frontend:AllowedOrigins"];
    if (!string.IsNullOrWhiteSpace(configuredOrigins))
    {
        return configuredOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    var frontendBaseUrl = configuration["Frontend:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
    {
        return new[] { frontendBaseUrl };
    }

    return new[] { "http://localhost:5173" };
}

static async Task SeedDomainDataAsync(AppDbContext db)
{
    if (!await db.Airplanes.AnyAsync())
    {
        db.Airplanes.AddRange(
            new Airplane
            {
                Name = "Jet Ranger",
                RegistrationNumber = "N172PT",
                Capacity = 6
            },
            new Airplane
            {
                Name = "Sky Hauler",
                RegistrationNumber = "N284PT",
                Capacity = 10
            },
            new Airplane
            {
                Name = "Falcon One",
                RegistrationNumber = "N391PT",
                Capacity = 8
            });
    }

    if (!await db.Personnel.AnyAsync())
    {
        db.Personnel.AddRange(
            new Personnel { FullName = "Avery Brooks" },
            new Personnel { FullName = "Jordan Ellis" },
            new Personnel { FullName = "Morgan Reyes" },
            new Personnel { FullName = "Taylor Quinn" });
    }

    await db.SaveChangesAsync();
}

public partial class Program { }
