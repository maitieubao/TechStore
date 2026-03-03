using FluentAssertions;
using TechStore.Domain.Common;

namespace TechStore.Tests.Common
{
    public class SlugHelperTests
    {
        [Theory]
        [InlineData("MacBook Pro 14 M4 Pro", "macbook-pro-14-m4-pro")]
        [InlineData("iPhone 16 Pro Max 256GB", "iphone-16-pro-max-256gb")]
        [InlineData("ASUS ROG Strix G16", "asus-rog-strix-g16")]
        [InlineData("Samsung Galaxy S24 Ultra", "samsung-galaxy-s24-ultra")]
        public void GenerateSlug_ShouldCreateCorrectSlug_ForEnglishNames(string input, string expected)
        {
            var result = SlugHelper.GenerateSlug(input);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Điện thoại", "dien-thoai")]
        [InlineData("Phụ kiện", "phu-kien")]
        [InlineData("Màn hình", "man-hinh")]
        [InlineData("Linh kiện PC", "linh-kien-pc")]
        [InlineData("Máy tính xách tay", "may-tinh-xach-tay")]
        [InlineData("Đồng hồ thông minh", "dong-ho-thong-minh")]
        public void GenerateSlug_ShouldHandleVietnameseCharacters(string input, string expected)
        {
            var result = SlugHelper.GenerateSlug(input);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("  Multiple   Spaces  ", "multiple-spaces")]
        [InlineData("Special @#$ Characters!", "special-characters")]
        [InlineData("--Leading-Trailing--", "leading-trailing")]
        [InlineData("", "")]
        public void GenerateSlug_ShouldHandleEdgeCases(string input, string expected)
        {
            var result = SlugHelper.GenerateSlug(input);
            result.Should().Be(expected);
        }
    }
}
