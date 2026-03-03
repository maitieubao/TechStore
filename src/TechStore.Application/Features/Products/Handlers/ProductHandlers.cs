using AutoMapper;
using MediatR;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Shared.DTOs;
using TechStore.Application.Features.Products.Commands;
using TechStore.Application.Features.Products.Queries;
using TechStore.Application.Features.Products.Specifications;

namespace TechStore.Application.Features.Products.Handlers
{
    // ===== Dynamic Filtering with Specification Pattern =====
    public class GetFilteredProductsHandler : IRequestHandler<GetFilteredProductsQuery, PagedResultDto<ProductDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetFilteredProductsHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResultDto<ProductDto>> Handle(GetFilteredProductsQuery request, CancellationToken cancellationToken)
        {
            var filter = request.Filter;
            var spec = new ProductFilterSpecification(filter);

            // Get total count (without paging)
            var totalCount = await _unitOfWork.Products.CountAsync(spec);

            // Get filtered, sorted, paged products
            var products = await _unitOfWork.Products.ListAsync(spec);

            // Map to DTO
            var items = products.Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                IsLowStock = p.StockQuantity > 0 && p.StockQuantity <= p.LowStockThreshold,
                Brand = p.Brand,
                Specifications = p.Specifications,
                CategoryId = p.CategoryId,
                CategoryName = p.Category?.Name,
                CategorySlug = p.Category?.Slug,
                AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => (double)r.Rating) : 0,
                ReviewCount = p.Reviews.Count,
                ImageUrls = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList(),
                IsActive = p.IsActive,
                IsCombo = p.IsCombo,
                OriginalPrice = p.OriginalPrice
            }).ToList();

            return new PagedResultDto<ProductDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }
    }

    // ===== Basic Query Handlers =====
    public class GetAllProductsHandler : IRequestHandler<GetAllProductsQuery, List<ProductDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetAllProductsHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<List<ProductDto>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
        {
            var products = await _unitOfWork.Products.GetAllAsync();
            return _mapper.Map<List<ProductDto>>(products);
        }
    }

    public class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, ProductDto?>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetProductByIdHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(request.Id);
            return product == null ? null : _mapper.Map<ProductDto>(product);
        }
    }

    // ===== Command Handlers =====
    public class CreateProductHandler : IRequestHandler<CreateProductCommand, int>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateProductHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            var slug = SlugHelper.GenerateSlug(request.Name);

            // Ensure unique slug
            var existingSlugCount = await _unitOfWork.Products.CountAsync(p => p.Slug.StartsWith(slug));
            if (existingSlugCount > 0)
                slug = $"{slug}-{existingSlugCount + 1}";

            var product = new Product
            {
                Name = request.Name,
                Slug = slug,
                Description = request.Description,
                Price = request.Price,
                StockQuantity = request.StockQuantity,
                Brand = request.Brand,
                Specifications = request.Specifications,
                CategoryId = request.CategoryId,
                IsCombo = request.IsCombo,
                OriginalPrice = request.IsCombo ? request.OriginalPrice : null
            };

            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.CompleteAsync();
            return product.Id;
        }
    }

    public class UpdateProductHandler : IRequestHandler<UpdateProductCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateProductHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(request.Id);
            if (product == null) return false;

            // Regenerate slug if name changed
            if (product.Name != request.Name)
            {
                var slug = SlugHelper.GenerateSlug(request.Name);
                var existingSlugCount = await _unitOfWork.Products.CountAsync(
                    p => p.Slug.StartsWith(slug) && p.Id != request.Id);
                if (existingSlugCount > 0)
                    slug = $"{slug}-{existingSlugCount + 1}";
                product.Slug = slug;
            }

            product.Name = request.Name;
            product.Description = request.Description;
            product.Price = request.Price;
            product.StockQuantity = request.StockQuantity;
            product.Brand = request.Brand;
            product.Specifications = request.Specifications;
            product.CategoryId = request.CategoryId;
            product.IsCombo = request.IsCombo;
            product.OriginalPrice = request.IsCombo ? request.OriginalPrice : null;
            product.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Products.Update(product);
            await _unitOfWork.CompleteAsync();
            return true;
        }
    }

    public class DeleteProductHandler : IRequestHandler<DeleteProductCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeleteProductHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(request.Id);
            if (product == null) return false;

            // Thay vì xoá cứng (Delete) khỏi DB gây ảnh hưởng đơn hàng lịch sử, ta sẽ Xóa mềm (Soft delete)
            // hoặc đơn giản là set IsActive = false;
            product.IsActive = false;
            product.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Products.Update(product);
            await _unitOfWork.CompleteAsync();
            return true;
        }
    }
}
