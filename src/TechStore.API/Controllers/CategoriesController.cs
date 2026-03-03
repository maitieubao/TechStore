using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller quản lý danh mục sản phẩm (Categories) trong hệ thống.
    /// 
    /// Chức năng chính:
    ///   - Lấy danh sách tất cả danh mục (public).
    ///   - Lấy chi tiết danh mục theo ID (public).
    ///   - Lấy danh mục theo slug SEO-friendly (public).
    ///   - Tạo mới danh mục với tự động tạo slug (Admin only).
    ///   - Cập nhật thông tin danh mục, tự động cập nhật slug nếu đổi tên (Admin only).
    ///   - Xóa danh mục (Admin only), có kiểm tra ràng buộc sản phẩm.
    /// 
    /// Route gốc: api/categories
    /// Phân quyền: Read → public; Create/Update/Delete → [Authorize(Roles = "Admin")].
    /// Slug được tạo tự động từ tên danh mục và đảm bảo tính duy nhất.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Khởi tạo controller với Unit of Work được inject qua DI.
        /// </summary>
        public CategoriesController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// [GET] api/categories
        /// Lấy danh sách tất cả danh mục sản phẩm.
        /// 
        /// Workflow:
        ///   1. Lấy toàn bộ categories từ database.
        ///   2. Map sang CategoryDto (Id, Name, Slug, Description, ImageUrl, ProductCount).
        ///   3. ProductCount được tính từ navigation property Products?.Count.
        ///   4. Trả về danh sách CategoryDto.
        /// 
        /// Public endpoint — không yêu cầu đăng nhập.
        /// Thường dùng để hiển thị menu danh mục hoặc filter sản phẩm.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<CategoryDto>>>> GetAll()
        {
            var categories = await _unitOfWork.Categories.GetAllAsync();
            var result = categories.Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                ProductCount = c.Products?.Count ?? 0
            }).ToList();

            return Ok(ApiResponse<List<CategoryDto>>.SuccessResponse(result));
        }

        /// <summary>
        /// [GET] api/categories/{id}
        /// Lấy chi tiết một danh mục theo ID số nguyên.
        /// 
        /// Workflow:
        ///   1. Tìm category theo id.
        ///   2. Nếu không tìm thấy → 404 Not Found.
        ///   3. Map sang CategoryDto và trả về.
        /// 
        /// Route param: {id:int} — ràng buộc route chỉ nhận số nguyên.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<CategoryDto>>> GetById(int id)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
                return NotFound(ApiResponse<CategoryDto>.ErrorResponse("Category not found"));

            var result = new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                ImageUrl = category.ImageUrl
            };

            return Ok(ApiResponse<CategoryDto>.SuccessResponse(result));
        }

        /// <summary>
        /// [GET] api/categories/slug/{slug}
        /// Lấy chi tiết danh mục theo slug (SEO-friendly URL).
        /// 
        /// Workflow:
        ///   1. Tìm category theo slug (ví dụ: "dien-thoai-di-dong").
        ///   2. Nếu không tìm thấy → 404 Not Found.
        ///   3. Map sang CategoryDto kèm ProductCount.
        /// 
        /// Route param: {slug} — chuỗi slug thân thiện SEO.
        /// Dùng để tạo URL dạng /categories/dien-thoai thay vì /categories/3.
        /// </summary>
        [HttpGet("slug/{slug}")]
        public async Task<ActionResult<ApiResponse<CategoryDto>>> GetBySlug(string slug)
        {
            var category = await _unitOfWork.Categories.FirstOrDefaultAsync(c => c.Slug == slug);
            if (category == null)
                return NotFound(ApiResponse<CategoryDto>.ErrorResponse("Category not found"));

            var result = new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                ProductCount = category.Products?.Count ?? 0
            };

            return Ok(ApiResponse<CategoryDto>.SuccessResponse(result));
        }

        /// <summary>
        /// [POST] api/categories
        /// Tạo mới một danh mục sản phẩm. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Yêu cầu Bearer JWT với role "Admin".
        ///   2. Nhận CreateCategoryDto (Name, Description, ImageUrl).
        ///   3. Tự động tạo slug từ tên danh mục bằng SlugHelper.GenerateSlug().
        ///      Ví dụ: "Điện Thoại Di Động" → "dien-thoai-di-dong".
        ///   4. Kiểm tra slug đã tồn tại chưa để đảm bảo tính duy nhất (400 nếu trùng).
        ///   5. Tạo entity Category và lưu vào database.
        ///   6. Trả về 201 Created kèm ID danh mục mới.
        /// 
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// Body: CreateCategoryDto { Name, Description, ImageUrl }
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<int>>> Create(CreateCategoryDto dto)
        {
            var slug = SlugHelper.GenerateSlug(dto.Name);

            // Đảm bảo slug là duy nhất trong hệ thống
            var existing = await _unitOfWork.Categories.AnyAsync(c => c.Slug == slug);
            if (existing)
                return BadRequest(ApiResponse<int>.ErrorResponse("A category with a similar name already exists"));

            var category = new Category
            {
                Name = dto.Name,
                Slug = slug,
                Description = dto.Description,
                ImageUrl = dto.ImageUrl
            };

            await _unitOfWork.Categories.AddAsync(category);
            await _unitOfWork.CompleteAsync();

            return CreatedAtAction(nameof(GetById), new { id = category.Id },
                ApiResponse<int>.SuccessResponse(category.Id, "Category created"));
        }

        /// <summary>
        /// [PUT] api/categories/{id}
        /// Cập nhật thông tin danh mục. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Yêu cầu Bearer JWT với role "Admin".
        ///   2. Tìm category theo id (404 nếu không tìm thấy).
        ///   3. Nếu tên thay đổi → tạo lại slug mới bằng SlugHelper.
        ///      Kiểm tra slug mới không trùng với category khác (loại trừ chính nó).
        ///   4. Cập nhật các trường: Name, Description, ImageUrl, Slug, UpdatedAt.
        ///   5. Lưu thay đổi vào database.
        /// 
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// Body: CreateCategoryDto { Name, Description, ImageUrl }
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> Update(int id, CreateCategoryDto dto)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
                return NotFound(ApiResponse<bool>.ErrorResponse("Category not found"));

            // Chỉ tạo lại slug nếu tên thay đổi
            if (category.Name != dto.Name)
            {
                var slug = SlugHelper.GenerateSlug(dto.Name);
                // Kiểm tra slug mới không trùng với category khác (c.Id != id)
                var duplicate = await _unitOfWork.Categories.AnyAsync(c => c.Slug == slug && c.Id != id);
                if (duplicate)
                    return BadRequest(ApiResponse<bool>.ErrorResponse("A category with a similar name already exists"));
                category.Slug = slug;
            }

            category.Name = dto.Name;
            category.Description = dto.Description;
            category.ImageUrl = dto.ImageUrl;
            category.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Categories.Update(category);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Category updated"));
        }

        /// <summary>
        /// [DELETE] api/categories/{id}
        /// Xóa một danh mục sản phẩm. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Yêu cầu Bearer JWT với role "Admin".
        ///   2. Tìm category theo id (404 nếu không tìm thấy).
        ///   3. Kiểm tra ràng buộc: nếu danh mục còn chứa sản phẩm → từ chối xóa (400).
        ///      Lý do: tránh mất dữ liệu, admin phải di chuyển hoặc xóa sản phẩm trước.
        ///   4. Nếu không có sản phẩm → xóa danh mục khỏi database.
        /// 
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// Lưu ý: Đây là hard delete — dữ liệu bị xóa vĩnh viễn.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
                return NotFound(ApiResponse<bool>.ErrorResponse("Category not found"));

            // Không cho xóa nếu còn sản phẩm trong danh mục
            var hasProducts = await _unitOfWork.Products.AnyAsync(p => p.CategoryId == id);
            if (hasProducts)
                return BadRequest(ApiResponse<bool>.ErrorResponse("Cannot delete category with existing products. Move or delete products first."));

            _unitOfWork.Categories.Delete(category);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Category deleted"));
        }
    }
}
