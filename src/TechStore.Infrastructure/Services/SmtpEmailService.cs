using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailService> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;

        public SmtpEmailService(
            IConfiguration configuration,
            ILogger<SmtpEmailService> logger,
            UserManager<AppUser> userManager,
            IUnitOfWork unitOfWork)
        {
            _configuration = configuration;
            _logger = logger;
            _userManager = userManager;
            _unitOfWork = unitOfWork;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var smtpUser = _configuration["Email:SmtpUser"] ?? "";
                var smtpPass = _configuration["Email:SmtpPass"] ?? "";
                var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@techstore.com";
                var fromName = _configuration["Email:FromName"] ?? "TechStore";

                if (string.IsNullOrEmpty(smtpUser))
                {
                    _logger.LogWarning("SMTP not configured, skipping email to {Email}: {Subject}", toEmail, subject);
                    return;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            }
        }

        public async Task SendOrderConfirmationAsync(int orderId, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user?.Email == null) return;

            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) return;

            var subject = $"TechStore - Order #{orderId} Confirmed!";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
                        <h1 style='color: white; margin: 0;'>🛒 TechStore</h1>
                    </div>
                    <div style='padding: 30px; background: #f9f9f9; border-radius: 0 0 10px 10px;'>
                        <h2>Order Confirmed! ✅</h2>
                        <p>Hello {user.FullName}, thank you for your order.</p>
                        <div style='background: white; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                            <p><strong>Order Number:</strong> #{orderId}</p>
                            <p><strong>Total Amount:</strong> {order.TotalAmount:N0} VNĐ</p>
                            <p><strong>Status:</strong> Pending</p>
                        </div>
                        <p>We will notify you when your order ships.</p>
                        <p style='color: #888; font-size: 12px;'>TechStore - Your Technology Partner</p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(user.Email, subject, body);
        }

        public async Task SendOrderStatusUpdateAsync(int orderId, string newStatus, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user?.Email == null) return;

            var statusEmoji = newStatus switch
            {
                "Pending" => "📋",
                "Shipping" => "🚚",
                "Delivered" => "📦",
                "Cancelled" => "❌",
                _ => "📋"
            };

            var subject = $"TechStore - Order #{orderId} {newStatus}";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
                        <h1 style='color: white; margin: 0;'>🛒 TechStore</h1>
                    </div>
                    <div style='padding: 30px; background: #f9f9f9; border-radius: 0 0 10px 10px;'>
                        <h2>{statusEmoji} Order Status Updated</h2>
                        <p>Hello {user.FullName},</p>
                        <div style='background: white; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                            <p><strong>Order Number:</strong> #{orderId}</p>
                            <p><strong>New Status:</strong> {newStatus}</p>
                        </div>
                        <p style='color: #888; font-size: 12px;'>TechStore - Your Technology Partner</p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(user.Email, subject, body);
        }
    }
}
