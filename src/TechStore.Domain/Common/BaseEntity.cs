using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TechStore.Domain.Common
{
    public abstract class BaseEntity
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Utility class for generating SEO-friendly slugs
    /// </summary>
    public static class SlugHelper
    {
        public static string GenerateSlug(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Remove Vietnamese diacritics
            text = RemoveDiacritics(text);

            // Convert to lowercase
            text = text.ToLowerInvariant();

            // Replace spaces with hyphens
            text = Regex.Replace(text, @"\s+", "-");

            // Remove invalid characters
            text = Regex.Replace(text, @"[^a-z0-9\-]", "");

            // Remove multiple consecutive hyphens
            text = Regex.Replace(text, @"-{2,}", "-");

            // Trim hyphens from start and end
            text = text.Trim('-');

            return text;
        }

        private static string RemoveDiacritics(string text)
        {
            // Vietnamese special replacements
            text = text.Replace("đ", "d").Replace("Đ", "D");

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
