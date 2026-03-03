namespace TechStore.Application.Common.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task SendOrderConfirmationAsync(int orderId, string userId);
        Task SendOrderStatusUpdateAsync(int orderId, string newStatus, string userId);
    }
}
