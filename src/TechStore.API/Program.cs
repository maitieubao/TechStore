using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using TechStore.API.Middlewares;
using TechStore.Application;
using TechStore.Infrastructure;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/techstore-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddControllers();

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "TechStore API",
            Version = "v1",
            Description = "API for TechStore - Technology Products Store"
        });

        // Add JWT Authentication to Swagger
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter 'Bearer' [space] and then your token.\n\nExample: \"Bearer eyJhbGciOiJIUzI1NiIs...\""
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Register Layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Hangfire
    var pgUrlHangfire = Environment.GetEnvironmentVariable("DATABASE_URL");
    builder.Services.AddHangfire(configuration =>
    {
        configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings();

        if (!string.IsNullOrEmpty(pgUrlHangfire))
        {
            var uri = new Uri(pgUrlHangfire);
            var pgConnStr = $"Host={uri.Host};Port={uri.Port};Database={uri.LocalPath.Substring(1)};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SslMode=Require;TrustServerCertificate=True;";
            configuration.UsePostgreSqlStorage(c => c.UseNpgsqlConnection(pgConnStr));
        }
        else
        {
            configuration.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"));
        }
    });

    builder.Services.AddHangfireServer();

    // JWT Authentication
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]
                    ?? "TechStoreDefaultSecretKey123456789012345678901234567890"))
        };
    });

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "TechStore API v1");
        });
    }

    // Hangfire Dashboard (Recommend securing this in production)
    app.UseHangfireDashboard("/hangfire");

    // Global Exception Middleware
    app.UseMiddleware<ExceptionMiddleware>();

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseStaticFiles(); // Serve uploaded images
    app.UseCors("AllowAll");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Seed default roles, admin user, and sample data
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<TechStore.Infrastructure.Persistence.AppDbContext>();
            await context.Database.EnsureCreatedAsync();

            await TechStore.Infrastructure.Identity.IdentitySeeder.SeedAsync(services);
            await TechStore.Infrastructure.Identity.DataSeeder.SeedDataAsync(services);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while seeding the database.");
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
