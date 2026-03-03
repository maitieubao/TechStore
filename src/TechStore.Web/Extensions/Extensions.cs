using System.Security.Claims;

namespace TechStore.Web.Extensions
{
    public static class ClaimExtensions
    {
        public static string? GetUserId(this ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.NameIdentifier);

        public static string? GetUserName(this ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.Name);

        public static string? GetEmail(this ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.Email);

        public static bool IsAdmin(this ClaimsPrincipal user)
            => user.IsInRole("Admin");
    }

    public static class CurrencyExtensions
    {
        public static string ToVnd(this decimal amount)
            => amount.ToString("N0") + " ₫";
    }
}
