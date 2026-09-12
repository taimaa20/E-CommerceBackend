using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Modules.Dashboard.Predicates;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Dashboard.Filters
{
    /// <summary>
    /// Global action filter — applied to every controller via
    /// <c>AddControllers(o =&gt; o.Filters.AddService&lt;DashboardFilterBindingFilter&gt;())</c>.
    /// For actions on a controller decorated with <see cref="DashboardScopeAttribute"/>
    /// it resolves the per-dashboard default preset against the bound
    /// <see cref="DashboardFilterDto"/> and stamps the scoped
    /// <see cref="IDashboardFilterContext"/> with the resulting window. For
    /// every other action it's a no-op.
    /// </summary>
    public sealed class DashboardFilterBindingFilter : IAsyncActionFilter
    {
        private readonly ILogger<DashboardFilterBindingFilter> _logger;
        private readonly IBranchContext _branchContext;
        private readonly ICurrentUserAccessor _currentUser;

        public DashboardFilterBindingFilter(
            ILogger<DashboardFilterBindingFilter> logger,
            IBranchContext branchContext,
            ICurrentUserAccessor currentUser)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
        {
            var scopeAttr = ResolveScopeAttribute(ctx);
            if (scopeAttr is null)
            {
                // Not a dashboard endpoint — passthrough.
                await next();
                return;
            }

            var contextAccessor = ctx.HttpContext.RequestServices.GetService(typeof(IDashboardFilterContext)) as IDashboardFilterContext;
            if (contextAccessor is null || contextAccessor.Initialised)
            {
                await next();
                return;
            }

            var filter = ctx.ActionArguments.Values
                .OfType<DashboardFilterDto>()
                .FirstOrDefault() ?? new DashboardFilterDto();

            try
            {
                var preset = filter.Preset == DashboardFilterPreset.Default
                    ? scopeAttr.DefaultPreset
                    : filter.Preset;
                var effective = filter.Clone();
                effective.Preset = preset;
                await ApplyBranchScopeAsync(effective, ctx.HttpContext.RequestAborted);

                var window = DashboardFilterResolver.Resolve(effective, scopeAttr.DefaultPreset);
                contextAccessor.Initialise(effective, window, scopeAttr.Scope);
            }
            catch (ArgumentException ex)
            {
                ctx.Result = new BadRequestObjectResult(new { message = ex.Message });
                return;
            }
            catch (Exception ex)
            {
                // Any other failure — log it and fall back to a safe Today window so
                // the calculators downstream still have a valid context.
                _logger.LogWarning(ex, "Dashboard filter binding failed for {Path}; falling back to default preset.", ctx.HttpContext.Request.Path);
                var fallback = new DashboardFilterDto { Preset = scopeAttr.DefaultPreset };
                await ApplyBranchScopeAsync(fallback, ctx.HttpContext.RequestAborted);
                var window = DashboardFilterResolver.Resolve(fallback, scopeAttr.DefaultPreset);
                contextAccessor.Initialise(fallback, window, scopeAttr.Scope);
            }

            await next();
        }

        private async Task ApplyBranchScopeAsync(DashboardFilterDto filter, CancellationToken ct)
        {
            if (filter.BranchId == Guid.Empty)
            {
                if (!_currentUser.IsAdminOrManager)
                    throw new ForbiddenException("All branches dashboard scope requires administrator or manager access.");

                filter.BranchId = null;
                return;
            }

            var branchContext = await _branchContext.GetCurrentAsync(ct);
            if (!filter.BranchId.HasValue)
            {
                filter.BranchId = branchContext.CurrentBranch.Id;
                return;
            }

            var selected = branchContext.AssignedBranches.FirstOrDefault(b => b.Id == filter.BranchId.Value);
            if (selected is null)
                throw new ForbiddenException("You are not assigned to the selected branch.");
            if (!selected.IsActive)
                throw new ValidationException("Inactive branches cannot be used as dashboard scope.");
        }

        /// <summary>
        /// EndpointMetadata is the canonical lookup, but for plain marker
        /// attributes on the controller class some configurations don't surface
        /// them there. Fall back to reflection on the controller type and the
        /// action method so the filter is reliable regardless.
        /// </summary>
        private static DashboardScopeAttribute? ResolveScopeAttribute(ActionExecutingContext ctx)
        {
            var fromMetadata = ctx.ActionDescriptor.EndpointMetadata
                .OfType<DashboardScopeAttribute>()
                .FirstOrDefault();
            if (fromMetadata is not null) return fromMetadata;

            if (ctx.ActionDescriptor is ControllerActionDescriptor cad)
            {
                var onMethod = (DashboardScopeAttribute?)Attribute.GetCustomAttribute(
                    cad.MethodInfo, typeof(DashboardScopeAttribute), inherit: true);
                if (onMethod is not null) return onMethod;

                var onClass = (DashboardScopeAttribute?)Attribute.GetCustomAttribute(
                    cad.ControllerTypeInfo, typeof(DashboardScopeAttribute), inherit: true);
                if (onClass is not null) return onClass;
            }
            return null;
        }
    }
}
