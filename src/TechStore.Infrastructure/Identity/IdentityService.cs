using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Caching.Memory;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Persistence;
using TechStore.Shared.DTOs;
using Google.Apis.Auth;

namespace TechStore.Infrastructure.Identity
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly IMemoryCache _memoryCache;
        private readonly IEmailService _emailService;

        public IdentityService(UserManager<AppUser> userManager, IConfiguration configuration, AppDbContext context, IMemoryCache memoryCache, IEmailService emailService)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _memoryCache = memoryCache;
            _emailService = emailService;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.UserName);
            if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            {
                return new AuthResponseDto
                {
                    IsSuccess = false,
                    Message = "Invalid username or password"
                };
            }

            var token = await GenerateJwtToken(user);
            var refreshToken = await GenerateRefreshToken(user);
            var roles = await _userManager.GetRolesAsync(user);

            return new AuthResponseDto
            {
                IsSuccess = true,
                Token = token,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiry = refreshToken.ExpiresAt,
                Message = "Login successful",
                UserName = user.UserName!,
                Email = user.Email!,
                Roles = roles.ToList()
            };
        }

        public async Task<AuthResponseDto> LoginWithGoogleAsync(string email, string fullName, string providerKey, string idToken)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _configuration["Google:ClientId"] }
                });

                email = payload.Email;
                fullName = payload.Name;
                providerKey = payload.Subject;
            }
            catch (Exception)
            {
                return new AuthResponseDto { IsSuccess = false, Message = "Google Token verification failed." };
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // Create user if they don't exist
                user = new AppUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return new AuthResponseDto { IsSuccess = false, Message = "Failed to create user account" };
                }

                await _userManager.AddToRoleAsync(user, "User");
                
                // Add Google login provider
                await _userManager.AddLoginAsync(user, new UserLoginInfo("Google", providerKey, "Google"));
            }
            else
            {
                // Link Google account if not already linked
                var logins = await _userManager.GetLoginsAsync(user);
                if (!logins.Any(l => l.LoginProvider == "Google"))
                {
                    await _userManager.AddLoginAsync(user, new UserLoginInfo("Google", providerKey, "Google"));
                }
            }

            var token = await GenerateJwtToken(user);
            var refreshToken = await GenerateRefreshToken(user);
            var roles = await _userManager.GetRolesAsync(user);

            return new AuthResponseDto
            {
                IsSuccess = true,
                Token = token,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiry = refreshToken.ExpiresAt,
                Message = "Login successful",
                UserName = user.UserName!,
                Email = user.Email!,
                Roles = roles.ToList()
            };
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            return await RegisterWithRoleAsync(dto, "User");
        }

        public async Task<AuthResponseDto> RegisterAdminAsync(RegisterDto dto)
        {
            return await RegisterWithRoleAsync(dto, "Admin");
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto dto)
        {
            // Validate expired JWT to get claims
            var principal = GetPrincipalFromExpiredToken(dto.Token);
            if (principal == null)
                return new AuthResponseDto { IsSuccess = false, Message = "Invalid access token" };

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return new AuthResponseDto { IsSuccess = false, Message = "Invalid token claims" };

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return new AuthResponseDto { IsSuccess = false, Message = "User not found" };

            // Validate refresh token
            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken && rt.UserId == userId);

            if (storedToken == null || !storedToken.IsActive)
                return new AuthResponseDto { IsSuccess = false, Message = "Invalid or expired refresh token" };

            // Revoke old refresh token
            storedToken.RevokedAt = DateTime.UtcNow;

            // Generate new tokens
            var newJwt = await GenerateJwtToken(user);
            var newRefreshToken = await GenerateRefreshToken(user);

            storedToken.ReplacedByToken = newRefreshToken.Token;
            await _context.SaveChangesAsync();

            var roles = await _userManager.GetRolesAsync(user);

            return new AuthResponseDto
            {
                IsSuccess = true,
                Token = newJwt,
                RefreshToken = newRefreshToken.Token,
                RefreshTokenExpiry = newRefreshToken.ExpiresAt,
                Message = "Token refreshed",
                UserName = user.UserName!,
                Email = user.Email!,
                Roles = roles.ToList()
            };
        }

        public async Task<bool> RevokeRefreshTokenAsync(string userId)
        {
            var tokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
                .ToListAsync();

            foreach (var token in tokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<string> ForgotPasswordAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return "If an account with that email exists, a password reset link has been sent.";

            var otp = new Random().Next(100000, 999999).ToString();
            
            _memoryCache.Set($"ForgotPasswordOtp_{email}", otp, TimeSpan.FromMinutes(10));

            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee;'>
                    <h2 style='color: #2c3e50;'>Yêu cầu đặt lại mật khẩu TechStore</h2>
                    <p>Chào bạn,</p>
                    <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản liên kết với địa chỉ email này. Nếu là bạn, vui lòng nhập mã OTP sau:</p>
                    <div style='background: #f9f9f9; padding: 15px; border-left: 4px solid #e74c3c; margin: 20px 0; font-size: 24px; font-weight: bold; letter-spacing: 5px; text-align: center;'>
                        {otp}
                    </div>
                    <p style='color: #7f8c8d; font-size: 14px;'>Mã này có hiệu lực trong 10 phút. Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                </div>";
            
            await _emailService.SendEmailAsync(email, "TechStore - Mã xác thực đặt lại mật khẩu", body);

            return "success";
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return false;

            if (!_memoryCache.TryGetValue($"ForgotPasswordOtp_{dto.Email}", out string? storedOtp) || storedOtp != dto.Token)
            {
                return false;
            }

            // We verified OTP, let's bypass Identity Token requirement
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, dto.NewPassword);
            
            if (result.Succeeded)
            {
                _memoryCache.Remove($"ForgotPasswordOtp_{dto.Email}");
            }

            return result.Succeeded;
        }

        private async Task<AuthResponseDto> RegisterWithRoleAsync(RegisterDto dto, string role)
        {
            var existingUser = await _userManager.FindByNameAsync(dto.UserName);
            if (existingUser != null)
            {
                return new AuthResponseDto
                {
                    IsSuccess = false,
                    Message = "Username already exists"
                };
            }

            var existingEmail = await _userManager.FindByEmailAsync(dto.Email);
            if (existingEmail != null)
            {
                return new AuthResponseDto
                {
                    IsSuccess = false,
                    Message = "Email already registered"
                };
            }

            var otp = new Random().Next(100000, 999999).ToString();
            
            _memoryCache.Set($"RegistrationOtp_{dto.Email}", new RegistrationCacheItem { Dto = dto, Otp = otp, Role = role }, TimeSpan.FromMinutes(10));

            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee;'>
                    <h2 style='color: #2c3e50;'>Xác nhận đăng ký tài khoản TechStore</h2>
                    <p>Chào <strong>{dto.FullName}</strong>,</p>
                    <p>Cảm ơn bạn đã đăng ký tài khoản tại TechStore. Để hoàn tất, vui lòng nhập mã OTP sau:</p>
                    <div style='background: #f9f9f9; padding: 15px; border-left: 4px solid #3498db; margin: 20px 0; font-size: 24px; font-weight: bold; letter-spacing: 5px; text-align: center;'>
                        {otp}
                    </div>
                    <p style='color: #7f8c8d; font-size: 14px;'>Mã này có hiệu lực trong 10 phút. Tuyệt đối không chia sẻ mã này cho bất kỳ ai.</p>
                </div>";
            
            await _emailService.SendEmailAsync(dto.Email, "TechStore - Mã xác thực đăng ký (OTP)", body);

            return new AuthResponseDto
            {
                IsSuccess = true,
                Message = "Registration pending verify otp",
                Email = dto.Email,
                UserName = dto.UserName
            };
        }

        public async Task<AuthResponseDto> VerifyOtpAsync(VerifyOtpDto dto)
        {
            if (!_memoryCache.TryGetValue($"RegistrationOtp_{dto.Email}", out RegistrationCacheItem? cacheData) || cacheData == null)
            {
                return new AuthResponseDto { IsSuccess = false, Message = "OTP không hợp lệ hoặc đã hết hạn." };
            }

            if (cacheData.Otp != dto.Otp)
            {
                return new AuthResponseDto { IsSuccess = false, Message = "Mã OTP không chính xác." };
            }

            var user = new AppUser
            {
                UserName = cacheData.Dto.UserName,
                Email = cacheData.Dto.Email,
                FullName = cacheData.Dto.FullName,
                PhoneNumber = cacheData.Dto.PhoneNumber,
                Address = cacheData.Dto.Address,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, cacheData.Dto.Password);
            if (!result.Succeeded)
            {
                return new AuthResponseDto
                {
                    IsSuccess = false,
                    Message = "Registration failed: " + string.Join(", ", result.Errors.Select(e => e.Description))
                };
            }

            await _userManager.AddToRoleAsync(user, cacheData.Role);
            _memoryCache.Remove($"RegistrationOtp_{dto.Email}");

            var token = await GenerateJwtToken(user);
            var refreshToken = await GenerateRefreshToken(user);

            return new AuthResponseDto
            {
                IsSuccess = true,
                Token = token,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiry = refreshToken.ExpiresAt,
                Message = "Registration successful",
                UserName = user.UserName!,
                Email = user.Email!,
                Roles = new List<string> { cacheData.Role }
            };
        }

        private class RegistrationCacheItem
        {
            public RegisterDto Dto { get; set; } = null!;
            public string Otp { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
        }

        private async Task<string> GenerateJwtToken(AppUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim("FullName", user.FullName)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"] ?? "TechStoreDefaultSecretKey123456789012345678901234567890"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expireMinutes = int.Parse(_configuration["Jwt:ExpireMinutes"] ?? "60");

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<RefreshToken> GenerateRefreshToken(AppUser user)
        {
            var refreshDays = int.Parse(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7");

            var refreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshDays)
            };

            // Remove old inactive tokens (cleanup)
            var oldTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == user.Id && (rt.RevokedAt != null || rt.ExpiresAt <= DateTime.UtcNow))
                .ToListAsync();
            _context.RefreshTokens.RemoveRange(oldTokens);

            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                    _configuration["Jwt:Key"] ?? "TechStoreDefaultSecretKey123456789012345678901234567890")),
                ValidateLifetime = false, // Allow expired tokens
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                    return null;

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
