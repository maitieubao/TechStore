using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller quản lý hồ sơ cá nhân của người dùng (User Profile).
    /// 
    /// Chức năng chính:
    ///   - Lấy thông tin hồ sơ cá nhân (FullName, Email, Phone, Address, Avatar).
    ///   - Cập nhật thông tin cá nhân (FullName, PhoneNumber, Address).
    ///   - Đổi mật khẩu (yêu cầu mật khẩu cũ để xác thực).
    ///   - Upload ảnh đại diện (avatar), tự động xóa ảnh cũ trước khi thay thế.
    /// 
    /// Route gốc: api/userprofile
    /// Phân quyền: [Authorize] — tất cả endpoints yêu cầu đăng nhập.
    ///             Mỗi user chỉ quản lý được hồ sơ của chính mình.
    /// 
    /// Sử dụng ASP.NET Identity UserManager để quản lý thông tin tài khoản.
    /// Ảnh đại diện được lưu qua IFileService trong thư mục "avatars/".
    /// Giới hạn kích thước avatar: 2MB.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserProfileController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IFileService _fileService;

        /// <summary>
        /// Khởi tạo controller với UserManager và FileService được inject qua DI.
        /// </summary>
        /// <param name="userManager">ASP.NET Identity UserManager để quản lý tài khoản.</param>
        /// <param name="fileService">Service upload/delete file cho ảnh đại diện.</param>
        public UserProfileController(UserManager<AppUser> userManager, IFileService fileService)
        {
            _userManager = userManager;
            _fileService = fileService;
        }

        /// <summary>
        /// Helper lấy UserId từ JWT Claims của người dùng đang đăng nhập.
        /// </summary>
        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>
        /// [GET] api/userprofile
        /// Lấy thông tin hồ sơ cá nhân của người dùng đang đăng nhập.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Tìm user qua UserManager.FindByIdAsync().
        ///   3. Nếu không tìm thấy → 404 Not Found.
        ///   4. Map sang UserProfileDto: Id, UserName, Email, FullName, PhoneNumber, Address, AvatarUrl, CreatedAt.
        ///   5. Trả về profile của user hiện tại.
        /// 
        /// Phân quyền: [Authorize] — user chỉ lấy được profile của chính mình.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetProfile()
        {
            var user = await _userManager.FindByIdAsync(GetUserId());
            if (user == null)
                return NotFound(ApiResponse<UserProfileDto>.ErrorResponse("User not found"));

            var profile = new UserProfileDto
            {
                Id = user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt
            };

            return Ok(ApiResponse<UserProfileDto>.SuccessResponse(profile));
        }

        /// <summary>
        /// [PUT] api/userprofile
        /// Cập nhật thông tin cá nhân của người dùng đang đăng nhập.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Tìm user qua UserManager (404 nếu không tìm thấy).
        ///   3. Cập nhật các trường: FullName, PhoneNumber, Address.
        ///   4. Gọi UserManager.UpdateAsync() với ASP.NET Identity validation.
        ///   5. Nếu thất bại (validation lỗi) → 400 Bad Request kèm danh sách lỗi.
        ///   6. Trả về 200 OK nếu cập nhật thành công.
        /// 
        /// Phân quyền: [Authorize] — user chỉ cập nhật được hồ sơ của chính mình.
        /// Body: UpdateProfileDto { FullName, PhoneNumber, Address }
        /// Lưu ý: Email và UserName không được cập nhật qua endpoint này.
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<ApiResponse<string>>> UpdateProfile(UpdateProfileDto dto)
        {
            var user = await _userManager.FindByIdAsync(GetUserId());
            if (user == null)
                return NotFound(ApiResponse<string>.ErrorResponse("User not found"));

            // Cập nhật thông tin cá nhân cho phép thay đổi
            user.FullName = dto.FullName;
            user.PhoneNumber = dto.PhoneNumber;
            user.Address = dto.Address;

            // Sử dụng Identity để cập nhật với đầy đủ validation
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(ApiResponse<string>.ErrorResponse(
                    string.Join("; ", result.Errors.Select(e => e.Description))));

            return Ok(ApiResponse<string>.SuccessResponse("Profile updated successfully"));
        }

        /// <summary>
        /// [PUT] api/userprofile/change-password
        /// Đổi mật khẩu người dùng. Yêu cầu nhập mật khẩu cũ để xác thực.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Tìm user qua UserManager (404 nếu không tìm thấy).
        ///   3. Gọi UserManager.ChangePasswordAsync(user, currentPassword, newPassword):
        ///      - Identity tự xác thực mật khẩu cũ.
        ///      - Áp dụng password policy (độ phức tạp, độ dài tối thiểu).
        ///   4. Nếu thất bại (sai mật khẩu cũ, password mới yếu) → 400 Bad Request.
        ///   5. Trả về 200 OK nếu đổi mật khẩu thành công.
        /// 
        /// Body: ChangePasswordDto { CurrentPassword, NewPassword }
        /// Phân quyền: [Authorize]
        /// Bảo mật: Không cho phép đổi mật khẩu mà không biết mật khẩu cũ.
        /// </summary>
        [HttpPut("change-password")]
        public async Task<ActionResult<ApiResponse<string>>> ChangePassword(ChangePasswordDto dto)
        {
            var user = await _userManager.FindByIdAsync(GetUserId());
            if (user == null)
                return NotFound(ApiResponse<string>.ErrorResponse("User not found"));

            // Identity tự xác thực mật khẩu cũ và áp dụng password policy cho mật khẩu mới
            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
                return BadRequest(ApiResponse<string>.ErrorResponse(
                    string.Join("; ", result.Errors.Select(e => e.Description))));

            return Ok(ApiResponse<string>.SuccessResponse("Password changed successfully"));
        }

        /// <summary>
        /// [POST] api/userprofile/avatar
        /// Upload ảnh đại diện (avatar) mới cho người dùng.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Tìm user qua UserManager (404 nếu không tìm thấy).
        ///   3. Kiểm tra file không rỗng (400 nếu thiếu file).
        ///   4. Kiểm tra kích thước file: tối đa 2MB (400 nếu vượt quá).
        ///   5. Nếu user có avatar cũ → gọi IFileService.DeleteFileAsync() để xóa file cũ.
        ///      → Tiết kiệm dung lượng storage, tránh file rác.
        ///   6. Upload file mới qua IFileService.UploadFileAsync() → nhận về URL ảnh.
        ///   7. Cập nhật AvatarUrl trong database qua UserManager.
        ///   8. Trả về URL ảnh đại diện mới.
        /// 
        /// Form: IFormFile file — file ảnh (multipart/form-data).
        /// Giới hạn: ≤ 2MB (nhỏ hơn ảnh sản phẩm vì avatar hiển thị nhỏ).
        /// Phân quyền: [Authorize]
        /// </summary>
        [HttpPost("avatar")]
        public async Task<ActionResult<ApiResponse<string>>> UploadAvatar(IFormFile file)
        {
            var user = await _userManager.FindByIdAsync(GetUserId());
            if (user == null)
                return NotFound(ApiResponse<string>.ErrorResponse("User not found"));

            if (file.Length == 0)
                return BadRequest(ApiResponse<string>.ErrorResponse("No file provided"));

            // Giới hạn kích thước avatar: 2MB
            if (file.Length > 2 * 1024 * 1024)
                return BadRequest(ApiResponse<string>.ErrorResponse("Avatar must be less than 2MB"));

            // Xóa avatar cũ trước khi upload mới (tránh dữ liệu rác trong storage)
            if (!string.IsNullOrEmpty(user.AvatarUrl))
                await _fileService.DeleteFileAsync(user.AvatarUrl);

            // Upload avatar mới vào thư mục "avatars/"
            using var stream = file.OpenReadStream();
            var avatarUrl = await _fileService.UploadFileAsync(stream, file.FileName, "avatars");

            // Cập nhật URL avatar mới trong database
            user.AvatarUrl = avatarUrl;
            await _userManager.UpdateAsync(user);

            return Ok(ApiResponse<string>.SuccessResponse(avatarUrl, "Avatar uploaded"));
        }
    }
}
