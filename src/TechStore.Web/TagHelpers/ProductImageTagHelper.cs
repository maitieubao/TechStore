using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;

namespace TechStore.Web.TagHelpers
{
    [HtmlTargetElement("product-img")]
    public class ProductImageTagHelper : TagHelper
    {
        private readonly IConfiguration _configuration;

        public ProductImageTagHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string? Src { get; set; }
        public string? Alt { get; set; }
        public string? Class { get; set; }
        public string? Style { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "img";
            output.Attributes.SetAttribute("class", Class);
            output.Attributes.SetAttribute("alt", Alt);
            
            if (!string.IsNullOrEmpty(Style))
            {
                output.Attributes.SetAttribute("style", Style);
            }

            var apiBaseUrl = _configuration["ApiBaseUrl"]?.TrimEnd('/');
            string finalSrc = "/images/no-image.png";

            if (!string.IsNullOrEmpty(Src))
            {
                if (Src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    finalSrc = Src;
                }
                else
                {
                    // Remove leading slash if present to avoid double slash
                    var cleanSrc = Src.TrimStart('/');
                    finalSrc = $"{apiBaseUrl}/{cleanSrc}";
                }
            }

            output.Attributes.SetAttribute("src", finalSrc);
            output.Attributes.SetAttribute("loading", "lazy");
        }
    }
}
