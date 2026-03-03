using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Entities;
using TechStore.Shared.Responses;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller quản lý hình ảnh sản phẩm (Product Images).
    /// 
    /// Chức năng chính:
    ///   - Upload hình ảnh cho một sản phẩm (Admin only), hỗ trợ đặt ảnh primary.
    ///   - Xóa hình ảnh sản phẩm (Admin only), đồng thời xóa file vật lý.
    ///   - Lấy danh sách ảnh của một sản phẩm theo thứ tự DisplayOrder (public).
    /// 
    /// Route gốc: api/images
    /// Phân quyền: Mặc định [Authorize(Roles = "Admin")]; endpoint GET images là [AllowAnonymous].
    /// 
    /// Giới hạn upload:
    ///   - Định dạng cho phép: .jpg, .jpeg, .png, .webp, .gif
    ///   - Kích thước tối đa: 5MB
    /// 
    /// Lưu ý: File được lưu qua IFileService với cấu trúc thư mục "products/{fileName}".
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class ImagesController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Khởi tạo controller với File Service và Unit of Work được inject qua DI.
        /// </summary>
        /// <param name="fileService">Service lưu trữ và xóa file vật lý (local/cloud).</param>
        /// <param name="unitOfWork">Unit of Work để truy cập repository sản phẩm và ảnh.</param>
        public ImagesController(IFileService fileService, IUnitOfWork unitOfWork)
        {
            _fileService = fileService;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// [POST] api/images/product/{productId}?isPrimary={bool}
        /// Upload hình ảnh mới cho một sản phẩm. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Kiểm tra sản phẩm tồn tại (404 nếu không tìm thấy).
        ///   2. Kiểm tra file không rỗng (400 nếu thiếu file).
        ///   3. Validate định dạng file: chỉ chấp nhận .jpg, .jpeg, .png, .webp, .gif.
        ///   4. Validate kích thước file: tối đa 5MB.
        ///   5. Upload file qua IFileService → nhận về URL ảnh.
        ///   6. Nếu isPrimary = true:
        ///      - Tìm tất cả ảnh primary hiện có của sản phẩm.
        ///      - Đặt IsPrimary = false cho các ảnh đó (chỉ có 1 ảnh primary tại một thời điểm).
        ///   7. Tạo entity ProductImage với DisplayOrder = số ảnh hiện có (append cuối).
        ///   8. Lưu vào database và trả về URL ảnh mới.
        /// 
        /// Route param: {productId} — ID sản phẩm cần upload ảnh.
        /// Query param: isPrimary — true nếu đây là ảnh chính hiển thị (mặc định false).
        /// Form: IFormFile file — file ảnh cần upload (multipart/form-data).
        /// </summary>
        [HttpPost("product/{productId}")]
        public async Task<ActionResult<ApiResponse<string>>> UploadProductImage(
            int productId, IFormFile file, [FromQuery] bool isPrimary = false)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return NotFound(ApiResponse<string>.ErrorResponse("Product not found"));

            if (file.Length == 0)
                return BadRequest(ApiResponse<string>.ErrorResponse("No file provided"));

            // Validate định dạng file
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(ApiResponse<string>.ErrorResponse("Invalid file type. Allowed: jpg, jpeg, png, webp, gif"));

            // Validate kích thước file (tối đa 5MB)
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(ApiResponse<string>.ErrorResponse("File size must be less than 5MB"));

            // Upload file lên storage
            using var stream = file.OpenReadStream();
            var imageUrl = await _fileService.UploadFileAsync(stream, file.FileName, "products");

            // Nếu đặt là ảnh primary → bỏ primary của các ảnh cũ
            if (isPrimary)
            {
                var existingImages = await _unitOfWork.ProductImages.FindAsync(
                    pi => pi.ProductId == productId && pi.IsPrimary);
                foreach (var img in existingImages)
                {
                    img.IsPrimary = false;
                    _unitOfWork.ProductImages.Update(img);
                }
            }

            // Tạo entity ProductImage với thứ tự hiển thị cuối cùng
            var productImage = new ProductImage
            {
                ProductId = productId,
                ImageUrl = imageUrl,
                IsPrimary = isPrimary,
                DisplayOrder = (await _unitOfWork.ProductImages.FindAsync(pi => pi.ProductId == productId)).Count
            };

            await _unitOfWork.ProductImages.AddAsync(productImage);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<string>.SuccessResponse(imageUrl, "Image uploaded successfully"));
        }

        /// <summary>
        /// [DELETE] api/images/{imageId}
        /// Xóa một hình ảnh sản phẩm. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Tìm ProductImage theo imageId (404 nếu không tìm thấy).
        ///   2. Gọi IFileService.DeleteFileAsync() để xóa file vật lý khỏi storage.
        ///   3. Xóa record ProductImage khỏi database.
        ///   4. Lưu thay đổi.
        /// 
        /// Route param: {imageId} — ID của ProductImage (không phải ProductId).
        /// Lưu ý: Cả file vật lý và record database đều bị xóa.
        /// </summary>
        [HttpDelete("{imageId}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteImage(int imageId)
        {
            var image = await _unitOfWork.ProductImages.GetByIdAsync(imageId);
            if (image == null)
                return NotFound(ApiResponse<bool>.ErrorResponse("Image not found"));

            // Xóa file vật lý khỏi storage trước
            await _fileService.DeleteFileAsync(image.ImageUrl);

            // Sau đó xóa record database
            _unitOfWork.ProductImages.Delete(image);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Image deleted"));
        }

        /// <summary>
        /// [GET] api/images/product/{productId}
        /// Lấy danh sách tất cả hình ảnh của một sản phẩm. Endpoint public.
        /// 
        /// Workflow:
        ///   1. Lấy tất cả ProductImage thuộc product có productId tương ứng.
        ///   2. Sắp xếp theo DisplayOrder (thứ tự hiển thị).
        ///   3. Trả về danh sách gồm: Id, ImageUrl, IsPrimary, DisplayOrder.
        /// 
        /// Route param: {productId} — ID sản phẩm cần lấy ảnh.
        /// Phân quyền: [AllowAnonymous] — override [Authorize(Roles = "Admin")] của controller.
        /// Dùng để hiển thị gallery ảnh sản phẩm trên trang chi tiết.
        /// </summary>
        [HttpGet("product/{productId}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<object>>>> GetProductImages(int productId)
        {
            var images = await _unitOfWork.ProductImages.FindAsync(pi => pi.ProductId == productId);
            var result = images
                .OrderBy(i => i.DisplayOrder) // Sắp xếp theo thứ tự DisplayOrder
                .Select(i => new
                {
                    i.Id,
                    i.ImageUrl,
                    i.IsPrimary,
                    i.DisplayOrder
                }).ToList();

            return Ok(ApiResponse<List<object>>.SuccessResponse(result.Cast<object>().ToList()));
        }
    }
}
