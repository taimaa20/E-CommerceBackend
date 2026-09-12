using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.Options;

namespace RestaurantPos.Api.Filters
{
    public class DeliveryPartnersFeatureFilter : IAsyncActionFilter
    {
        private readonly IOptions<DeliveryPartnersOptions> _options;

        public DeliveryPartnersFeatureFilter(IOptions<DeliveryPartnersOptions> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!_options.Value.Enabled)
            {
                context.Result = new NotFoundObjectResult(new { message = "Delivery partners feature is disabled." });
                return;
            }

            await next();
        }
    }
}
