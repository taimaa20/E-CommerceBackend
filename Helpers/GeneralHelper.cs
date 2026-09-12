namespace RestaurantPos.Api.Helpers
{
    public static class GeneralHelper
    {
        public static bool IsArabicRequested(HttpRequest request)
        {
            var language = request.Headers["Accept-Language"].ToString();
            return language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        }
        public static string ResolveDisplayName(string name, string? nameAr, bool isArabic)
        {
            return isArabic && !string.IsNullOrWhiteSpace(nameAr) ? nameAr : name;
        }
    }
}
