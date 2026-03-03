using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Common.Interfaces;
using TechStore.Shared.DTOs;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller xử lý form liên hệ (Contact Form) từ người dùng.
    /// 
    /// Chức năng chính:
    ///   - Nhận tin nhắn liên hệ từ người dùng (tên, email, nội dung).
    ///   - Gửi email thông báo đến địa chỉ quản trị viên của TechStore.
    ///   - Format email dưới dạng HTML cho dễ đọc.
    /// 
    /// Route gốc: api/contact
    /// Phân quyền: Public — không yêu cầu đăng nhập.
    /// Email đích: baomttd01287@gmail.com (cấu hình cứng trong code).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly IEmailService _emailService;

        /// <summary>
        /// Khởi tạo controller với Email Service được inject qua DI.
        /// </summary>
        public ContactController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        /// <summary>
        /// [POST] api/contact
        /// Gửi tin nhắn liên hệ từ người dùng đến email quản trị viên.
        /// 
        /// Workflow:
        ///   1. Nhận ContactDto (Name, Email, Message) từ request body.
        ///   2. Kiểm tra ModelState validation (400 nếu dữ liệu không hợp lệ).
        ///   3. Tạo subject theo định dạng: "[TechStore] Tin nhắn từ {Name}".
        ///   4. Tạo body HTML với thông tin: tên, email người gửi, và nội dung tin nhắn.
        ///   5. Gọi IEmailService.SendEmailAsync() để gửi email đến admin.
        ///   6. Trả về 200 OK với thông báo thành công.
        /// 
        /// Body: ContactDto { Name, Email, Message }
        /// Email được gửi đến: baomttd01287@gmail.com
        /// Lưu ý: Endpoint này không lưu tin nhắn vào database, chỉ gửi email trực tiếp.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Contact([FromBody] ContactDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Tạo tiêu đề email
            var subject = $"[TechStore] Tin nhắn từ {dto.Name}";

            // Tạo nội dung email dạng HTML
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee;'>
                    <h2 style='color: #2c3e50;'>Tin nhắn liên hệ mới</h2>
                    <p><strong>Người gửi:</strong> {dto.Name}</p>
                    <p><strong>Email:</strong> {dto.Email}</p>
                    <div style='background: #f9f9f9; padding: 15px; border-left: 4px solid #3498db; margin-top: 20px;'>
                        <p style='margin: 0; white-space: pre-wrap;'>{dto.Message}</p>
                    </div>
                </body>
                </html>";

            // Gửi email đến địa chỉ admin của TechStore
            await _emailService.SendEmailAsync("baomttd01287@gmail.com", subject, body);

            return Ok(new { success = true, message = "Gửi liên hệ thành công" });
        }
    }
}
