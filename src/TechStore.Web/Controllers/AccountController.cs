using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TechStore.Web.Models.ViewModels;
using TechStore.Web.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace TechStore.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthApiService _authService;
        private readonly IConfiguration _configuration;

        public AccountController(IAuthApiService authService, IConfiguration configuration)
        {
            _authService = authService;
            _configuration = configuration;
        }

        // GET: /account/login
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                return RedirectToAction("Index", "Home", new { area = "" });
            }

            ViewData["GoogleClientId"] = _configuration["Google:ClientId"] ?? "";
            return View(new LoginVM { ReturnUrl = returnUrl });
        }

        // POST: /account/login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var result = await _authService.LoginAsync(vm.UserName, vm.Password);
            if (result == null || !result.IsSuccess)
            {
                ModelState.AddModelError("", result?.Message ?? "Đăng nhập thất bại");
                return View(vm);
            }

            // Parse JWT and create cookie claims
            await SignInWithToken(result.Token, result.RefreshToken);

            // Store JWT in cookie for API calls
            Response.Cookies.Append("TechStore_Token", result.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Set true in production
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            if (result.Roles != null && result.Roles.Contains("Admin"))
            {
                if (string.IsNullOrEmpty(vm.ReturnUrl) || !Url.IsLocalUrl(vm.ReturnUrl))
                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }

            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToAction("Index", "Home", new { area = "" });
        }

        // GET: /account/register
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                return RedirectToAction("Index", "Home", new { area = "" });
            }

            return View(new RegisterVM());
        }

        // POST: /account/register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (vm.Password != vm.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Mật khẩu xác nhận không khớp");
                return View(vm);
            }

            var result = await _authService.RegisterAsync(new TechStore.Shared.DTOs.RegisterDto
            {
                UserName = vm.UserName,
                Email = vm.Email,
                Password = vm.Password,
                FullName = vm.FullName,
                PhoneNumber = vm.PhoneNumber,
                Address = vm.Address
            });
            if (result == null || !result.IsSuccess)
            {
                ModelState.AddModelError("", result?.Message ?? "Đăng ký thất bại");
                return View(vm);
            }

            TempData["RegisterEmail"] = vm.Email;
            return RedirectToAction("VerifyOtp");
        }

        // GET: /account/verifyotp
        public IActionResult VerifyOtp()
        {
            var email = TempData["RegisterEmail"]?.ToString();
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Register");
            }

            return View(new VerifyOtpVM { Email = email });
        }

        // POST: /account/verifyotp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(VerifyOtpVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var result = await _authService.VerifyOtpAsync(vm.Email, vm.Otp);
            if (result == null || !result.IsSuccess)
            {
                ModelState.AddModelError("", result?.Message ?? "Mã OTP không hợp lệ");
                TempData["RegisterEmail"] = vm.Email; // Keep it alive
                return View(vm);
            }

            // Auto sign-in after successful verification
            await SignInWithToken(result.Token, result.RefreshToken);

            Response.Cookies.Append("TechStore_Token", result.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            TempData["Success"] = "Đăng ký thành công!";
            
            if (result.Roles != null && result.Roles.Contains("Admin"))
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        // POST: /account/logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("TechStore_Token");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // GET: /account/forgotpassword
        public IActionResult ForgotPassword()
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                return RedirectToAction("Index", "Home", new { area = "" });
            }

            return View(new ForgotPasswordVM());
        }

        // POST: /account/forgotpassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVM vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var resultToken = await _authService.ForgotPasswordAsync(vm.Email);
            
            // In a real app we would send the email here or in the API. 
            // In our current implementation, the API handles the email sending and we just show success.
            if (string.IsNullOrEmpty(resultToken))
            {
                // To prevent email enumeration, we should still show success, but for demo:
                ModelState.AddModelError("", "Không tìm thấy email hoặc có lỗi xảy ra.");
                return View(vm);
            }

            TempData["ResetEmail"] = vm.Email;
            return RedirectToAction("ResetPassword");
        }

        // GET: /account/resetpassword
        public IActionResult ResetPassword()
        {
            var email = TempData["ResetEmail"]?.ToString();
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("ForgotPassword");
            }

            return View(new ResetPasswordVM { Email = email });
        }

        // POST: /account/resetpassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            // API expects 'token' but in our verification flow we send 'otp' inside token field implicitly
            // We pass it down to ResetPassword API
            var success = await _authService.ResetPasswordAsync(vm.Email, vm.Token, vm.NewPassword);
            
            if (!success)
            {
                ModelState.AddModelError("", "Mã OTP không hợp lệ hoặc đã hết hạn.");
                TempData["ResetEmail"] = vm.Email; // keep it alive
                return View(vm);
            }

            TempData["Success"] = "Đổi mật khẩu thành công! Bạn có thể đăng nhập ngay.";
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied() => View();

        // POST: /account/googlecallback
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GoogleCallback(string credential)
        {
            if (string.IsNullOrEmpty(credential))
            {
                TempData["Error"] = "Đăng nhập Google thất bại.";
                return RedirectToAction("Login");
            }

            try
            {
                // Decode Google JWT without verifying signature (for simplicity)
                // In production, verify against Google's public keys
                var handler = new JwtSecurityTokenHandler();
                var googleJwt = handler.ReadJwtToken(credential);

                var email = googleJwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
                var name = googleJwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
                var sub = googleJwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(sub))
                {
                    TempData["Error"] = "Không lấy được thông tin từ Google.";
                    return RedirectToAction("Login");
                }

                var result = await _authService.GoogleLoginAsync(email, name ?? email, sub, credential);
                if (result == null || !result.IsSuccess)
                {
                    TempData["Error"] = result?.Message ?? "Đăng nhập Google thất bại.";
                    return RedirectToAction("Login");
                }

                await SignInWithToken(result.Token, result.RefreshToken);

                Response.Cookies.Append("TechStore_Token", result.Token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = false,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });

                if (result.Roles != null && result.Roles.Contains("Admin"))
                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

                return RedirectToAction("Index", "Home", new { area = "" });
            }
            catch (Exception)
            {
                TempData["Error"] = "Có lỗi xảy ra khi đăng nhập bằng Google.";
                return RedirectToAction("Login");
            }
        }

        // === Private Helpers ===
        private async Task SignInWithToken(string token, string? refreshToken)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            var claims = new List<Claim>(jwt.Claims);

            // Ensure role claim is mapped correctly
            var roleClaims = jwt.Claims.Where(c =>
                c.Type == "role" || c.Type == ClaimTypes.Role).ToList();

            foreach (var role in roleClaims)
            {
                if (role.Type == "role")
                    claims.Add(new Claim(ClaimTypes.Role, role.Value));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });
        }
    }
}
