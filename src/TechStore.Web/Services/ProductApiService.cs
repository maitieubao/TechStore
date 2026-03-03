using TechStore.Shared.DTOs;
using TechStore.Web.Services.Base;
using TechStore.Web.Services.Interfaces;

namespace TechStore.Web.Services
{
    public class ProductApiService : BaseApiService, IProductApiService
    {
        public ProductApiService(HttpClient httpClient, IHttpContextAccessor accessor)
            : base(httpClient, accessor) { }

        public async Task<PagedResultDto<ProductDto>?> GetProductsAsync(ProductFilterDto filter)
        {
            var queryParams = new List<string>
            {
                $"page={filter.Page}",
                $"pageSize={filter.PageSize}"
            };

            if (!string.IsNullOrEmpty(filter.Search)) queryParams.Add($"search={Uri.EscapeDataString(filter.Search)}");
            if (filter.CategoryId.HasValue) queryParams.Add($"categoryId={filter.CategoryId}");
            if (!string.IsNullOrEmpty(filter.Brand)) queryParams.Add($"brand={Uri.EscapeDataString(filter.Brand)}");
            if (filter.MinPrice.HasValue) queryParams.Add($"minPrice={filter.MinPrice}");
            if (filter.MaxPrice.HasValue) queryParams.Add($"maxPrice={filter.MaxPrice}");
            if (filter.MinRating.HasValue) queryParams.Add($"minRating={filter.MinRating}");
            if (filter.InStock.HasValue) queryParams.Add($"inStock={filter.InStock}");
            if (filter.IsActive.HasValue) queryParams.Add($"isActive={filter.IsActive}");
            if (filter.IsCombo.HasValue) queryParams.Add($"isCombo={filter.IsCombo}");
            if (!string.IsNullOrEmpty(filter.SortBy)) queryParams.Add($"sortBy={filter.SortBy}");
            if (filter.SortDescending) queryParams.Add("sortDescending=true");

            var url = $"api/products?{string.Join("&", queryParams)}";
            var result = await GetAsync<PagedResultDto<ProductDto>>(url);
            return result?.Data;
        }

        public async Task<ProductDto?> GetProductByIdAsync(int id)
        {
            var result = await GetAsync<ProductDto>($"api/products/{id}");
            return result?.Data;
        }

        public async Task<ProductDto?> GetProductBySlugAsync(string slug)
        {
            var result = await GetAsync<ProductDto>($"api/products/slug/{slug}");
            return result?.Data;
        }

        public async Task<List<string>?> GetBrandsAsync()
        {
            var result = await GetAsync<List<string>>("api/products/brands");
            return result?.Data;
        }

        public async Task<(decimal Min, decimal Max)?> GetPriceRangeAsync()
        {
            var result = await GetAsync<PriceRangeResult>("api/products/price-range");
            if (result?.Data != null)
                return (result.Data.Min, result.Data.Max);
            return null;
        }

        public async Task<int> CreateProductAsync(TechStore.Web.Models.ViewModels.CreateProductVM model)
        {
            var result = await PostAsync<int>("api/products", model);
            return result?.Data ?? 0;
        }

        public async Task<bool> UpdateProductAsync(int id, TechStore.Web.Models.ViewModels.CreateProductVM model)
        {
            var result = await PutAsync<bool>($"api/products/{id}", model);
            return result?.Success ?? false;
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            var result = await DeleteAsync<bool>($"api/products/{id}");
            return result?.Success ?? false;
        }

        public async Task<bool> UploadImageAsync(int productId, Microsoft.AspNetCore.Http.IFormFile file, bool isPrimary = false)
        {
            using var content = new MultipartFormDataContent();
            var stream = file.OpenReadStream();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "file", file.FileName);

            var result = await PostFormAsync<string>($"api/images/product/{productId}?isPrimary={isPrimary}", content);
            return result?.Success ?? false;
        }
    }

    // Helper class for price range deserialization
    public class PriceRangeResult
    {
        public decimal Min { get; set; }
        public decimal Max { get; set; }
    }
}
