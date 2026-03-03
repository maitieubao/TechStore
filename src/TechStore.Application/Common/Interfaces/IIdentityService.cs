using TechStore.Shared.DTOs;

namespace TechStore.Application.Common.Interfaces
{
    public interface IIdentityService
    {
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> RegisterAdminAsync(RegisterDto dto);
        Task<AuthResponseDto> VerifyOtpAsync(VerifyOtpDto dto);
        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto dto);
        Task<bool> RevokeRefreshTokenAsync(string userId);
        Task<string> ForgotPasswordAsync(string email);
        Task<bool> ResetPasswordAsync(ResetPasswordDto dto);
        Task<AuthResponseDto> LoginWithGoogleAsync(string email, string fullName, string providerKey, string idToken);
    }
}
