using Microsoft.Extensions.Primitives;

namespace RestaurantPos.Api.Services
{
    public class HeaderCurrentBranchProvider : ICurrentBranchProvider
    {
        public const string BranchHeaderName = "X-Branch-ID";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public HeaderCurrentBranchProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public Guid? GetSelectedBranchId()
        {
            var context = _httpContextAccessor.HttpContext;
            StringValues value;
            if (context?.Request.Headers.TryGetValue(BranchHeaderName, out value) != true &&
                context?.Request.Query.TryGetValue("branchId", out value) != true)
            {
                return null;
            }

            return Guid.TryParse(value.FirstOrDefault(), out var branchId) && branchId != Guid.Empty
                ? branchId
                : null;
        }
    }
}
