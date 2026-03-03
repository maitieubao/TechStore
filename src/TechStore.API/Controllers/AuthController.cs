using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Common.Interfaces;
using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller xử lý toàn bộ luồng xác thực người dùng (Authentication).
    /// 
    /// Chức năng chính:
    ///   - Đăng ký tài khoản người dùng thường và admin.
    ///   - Xác thực OTP sau khi đăng ký.
    ///   - Đăng nhập và cấp phát JWT + Refresh Token.
    ///   - Làm mới JWT bằng Refresh Token (khi JWT hết hạn).
    ///   - Thu hồi Refresh Token (đăng xuất toàn bộ thiết bị).
    ///   - Quên mật khẩu và đặt lại mật khẩu.
    /// 
    /// Route gốc: api/auth
    /// Không yêu cầu xác thực cho hầu hết endpoints (trừ register-admin, revoke-token).
    /// Tất cả logic nghiệp vụ được ủy thác cho <see cref="IIdentityService"/>.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IIdentityService _identityService;

        /// <summary>
        /// Khởi tạo controller với Identity Service được inject qua DI.
        /// </summary>
        public AuthController(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        /// <summary>
        /// [POST] api/auth/login
        /// Đăng nhập bằng email và mật khẩu.
        /// 
        /// Workflow:
        ///   1. Nhận LoginDto (email, password).
        ///   2. Gọi IIdentityService.LoginAsync() để xác thực.
        ///   3. Nếu thành công → trả về JWT Access Token + Refresh Token.
        ///   4. Nếu thất bại → trả về 400 Bad Request kèm thông báo lỗi.
        /// 
        /// Response: ApiResponse&lt;AuthResponseDto&gt; chứa accessToken, refreshToken, thông tin user.
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(LoginDto dto)
        {
            var result = await _identityService.LoginAsync(dto);
            if (!result.IsSuccess)
                return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse(result.Message));

            return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, "Login successful"));
        }

        [HttpPost("google-login")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            var result = await _identityService.LoginWithGoogleAsync(dto.Email, dto.FullName, dto.ProviderKey, dto.IdToken);
            if (!result.IsSuccess)
                return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse(result.Message));

            return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, "Google login successful"));
        }

        /// <summary>
        /// [POST] api/auth/register
        /// Đăng ký tài khoản người dùng mới.
        /// 
        /// Workflow:
        ///   1. Nhận RegisterDto (username, email, password).
        ///   2. Gọi IIdentityService.RegisterAsync() để tạo tài khoản.
        ///   3. Hệ thống gửi mã OTP qua email để xác thực.
        ///   4. Nếu thành công → trả về thông báo "OTP sent" (tài khoản chờ xác thực).
        ///   5. Nếu thất bại (email đã tồn tại, v.v.) → 400 Bad Request.
        /// 
        /// Lưu ý: Tài khoản chưa được kích hoạt cho đến khi OTP được xác minh (gọi verify-otp).
        /// </summary>
        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register(RegisterDto dto)
        {
            var result = await _identityService.RegisterAsync(dto);
            if (!result.IsSuccess)
                return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse(result.Message));

            return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, "OTP sent"));
        }

        /// <summary>
        /// [POST] api/auth/verify-otp
        /// Xác minh mã OTP sau khi đăng ký để kích hoạt tài khoản.
        /// 
        /// Workflow:
        ///   1. Nhận VerifyOtpDto (email, otp).
        ///   2. Gọi IIdentityService.VerifyOtpAsync() để kiểm tra OTP.
        ///   3. Nếu OTP hợp lệ → tài khoản được kích hoạt, trả về JWT + Refresh Token.
        ///   4. Nếu OTP sai hoặc hết hạn → 400 Bad Request.
        /// </summary>
        [HttpPost("verify-otp")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> VerifyOtp(VerifyOtpDto dto)
        {
            var result = await _identityService.VerifyOtpAsync(dto);
            if (!result.IsSuccess)
                return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse(result.Message));

            return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, "Registration successful"));
        }

        /// <summary>
        /// [POST] api/auth/register-admin
        /// Tạo tài khoản Admin mới. Chỉ Admin hiện tại mới có quyền gọi endpoint này.
        /// 
        /// Workflow:
        ///   1. Yêu cầu Bearer JWT với role "Admin".
        ///   2. Nhận RegisterDto (username, email, password).
        ///   3. Gọi IIdentityService.RegisterAdminAsync() để tạo tài khoản với role Admin.
        ///   4. Trả về thông tin tài khoản Admin vừa tạo.
        /// 
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// </summary>
        [HttpPost("register-admin")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RegisterAdmin(RegisterDto dto)
        {
            var result = await _identityService.RegisterAdminAsync(dto);
            if (!result.IsSuccess)
                return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse(result.Message));

            return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, "Admin registration successful"));
        }

        /// <summary>
        /// [POST] api/auth/refresh-token
        /// Làm mới cặp JWT + Refresh Token khi JWT cũ đã hết hạn.
        /// 
        /// Workflow:
        ///   1. Nhận RefreshTokenDto (expiredAccessToken, refreshToken).
        ///   2. Gọi IIdentityService.RefreshTokenAsync() để xác thực refresh token.
        ///   3. Nếu hợp lệ → cấp JWT mới + Refresh Token mới (rotation).
        ///   4. Nếu refresh token hết hạn hoặc bị thu hồi → 401 Unauthorized.
        /// 
        /// Lưu ý: Refresh Token chỉ dùng được 1 lần (Token Rotation để bảo mật).
        /// </summary>
        [HttpPost("refresh-token")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshToken(RefreshTokenDto dto)
        {
            var result = await _identityService.RefreshTokenAsync(dto);
            if (!result.IsSuccess)
                return Unauthorized(ApiResponse<AuthResponseDto>.ErrorResponse(result.Message));

            return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, "Token refreshed"));
        }

        /// <summary>
        /// [POST] api/auth/revoke-token
        /// Thu hồi tất cả Refresh Token của người dùng hiện tại (đăng xuất toàn thiết bị).
        /// 
        /// Workflow:
        ///   1. Yêu cầu Bearer JWT hợp lệ.
        ///   2. Lấy UserId từ Claims trong JWT.
        ///   3. Gọi IIdentityService.RevokeRefreshTokenAsync() để vô hiệu hóa toàn bộ refresh token.
        ///   4. Sau khi thu hồi, người dùng phải đăng nhập lại trên tất cả thiết bị.
        /// 
        /// Phân quyền: [Authorize] — yêu cầu đăng nhập.
        /// </summary>
        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> RevokeToken()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _identityService.RevokeRefreshTokenAsync(userId);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "All refresh tokens revoked"));
        }

        /// <summary>
        /// [POST] api/auth/forgot-password
        /// Yêu cầu đặt lại mật khẩu (gửi link/token reset qua email).
        /// 
        /// Workflow:
        ///   1. Nhận ForgotPasswordDto (email).
        ///   2. Gọi IIdentityService.ForgotPasswordAsync() để tạo reset token.
        ///   3. Token được gửi qua email (trong môi trường production).
        ///   4. Luôn trả về 200 OK dù email có tồn tại hay không
        ///      → Tránh email enumeration attack (kẻ tấn công dò email hợp lệ).
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<ActionResult<ApiResponse<string>>> ForgotPassword(ForgotPasswordDto dto)
        {
            var result = await _identityService.ForgotPasswordAsync(dto.Email);
            // Always return success to prevent email enumeration
            return Ok(ApiResponse<string>.SuccessResponse(result,
                "If an account with that email exists, a password reset link has been sent."));
        }

        /// <summary>
        /// [POST] api/auth/reset-password
        /// Đặt lại mật khẩu bằng token nhận được từ forgot-password.
        /// 
        /// Workflow:
        ///   1. Nhận ResetPasswordDto (email, token, newPassword).
        ///   2. Gọi IIdentityService.ResetPasswordAsync() để xác thực token và cập nhật mật khẩu.
        ///   3. Nếu token hợp lệ → mật khẩu được cập nhật, trả về 200 OK.
        ///   4. Nếu token không hợp lệ hoặc hết hạn → 400 Bad Request.
        /// 
        /// Lưu ý: Reset token chỉ dùng được 1 lần và có thời hạn.
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<ActionResult<ApiResponse<bool>>> ResetPassword(ResetPasswordDto dto)
        {
            var result = await _identityService.ResetPasswordAsync(dto);
            if (!result)
                return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid token or email"));

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Password reset successfully"));
        }
    }
}
