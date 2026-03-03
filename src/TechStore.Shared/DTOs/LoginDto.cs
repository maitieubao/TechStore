namespace TechStore.Shared.DTOs
{
    public class LoginDto
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    
    public class GoogleLoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ProviderKey { get; set; } = string.Empty;
        public string IdToken { get; set; } = string.Empty;
    }
}
