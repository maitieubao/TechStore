using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;
using TechStore.Infrastructure.Persistence;
using TechStore.Infrastructure.Repositories;
using TechStore.Infrastructure.Services;
using TechStore.Infrastructure.Payments;
using Microsoft.AspNetCore.Identity;

namespace TechStore.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMemoryCache();

            // Auto-detect database provider:
            // - If DATABASE_URL env var is set (Render PostgreSQL) → use Npgsql
            // - Otherwise → use SQL Server (local / SmarterASP)
            var pgUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            services.AddDbContext<AppDbContext>(options =>
            {
                if (!string.IsNullOrEmpty(pgUrl))
                {
                    // Render PostgreSQL: convert DATABASE_URL to Npgsql format
                    var uri = new Uri(pgUrl);
                    var port = uri.Port > 0 ? uri.Port : 5432;
                    var pgConnStr = $"Host={uri.Host};Port={port};Database={uri.LocalPath.Substring(1)};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SslMode=Require;TrustServerCertificate=True;";

                    options.UseNpgsql(pgConnStr,
                        b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
                }
                else
                {
                    options.UseSqlServer(
                        configuration.GetConnectionString("DefaultConnection"),
                        b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
                }
            });

            services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

            // Repositories
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IOrderRepository, OrderRepository>();

            // Services
            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IFileService, LocalFileService>();
            services.AddScoped<IEmailService, SmtpEmailService>();
            services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();
            services.AddScoped<IPaymentService, PayOSService>();

            // PayOS: PayOSService sử dụng payOS NuGet package v2.x
            // Cấu hình tại appsettings.json section "PayOS": ClientId, ApiKey, ChecksumKey, ReturnUrl, CancelUrl

            return services;
        }
    }
}
