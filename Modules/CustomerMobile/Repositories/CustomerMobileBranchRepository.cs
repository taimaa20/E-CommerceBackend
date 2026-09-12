using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.CustomerMobile.Repositories
{
    public interface ICustomerMobileBranchRepository
    {
        Task<(List<Branch> Items, int TotalCount)> GetActiveAsync(
            Guid tenantId,
            int page,
            int pageSize,
            CancellationToken ct);

        Task<bool> IsActiveAsync(Guid tenantId, Guid branchId, CancellationToken ct);
        Task<List<PaymentMethod>> GetPaymentMethodsAsync(Guid tenantId, Guid branchId, CancellationToken ct);
    }

    public class CustomerMobileBranchRepository : ICustomerMobileBranchRepository
    {
        private readonly PosDbContext _context;

        public CustomerMobileBranchRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<(List<Branch> Items, int TotalCount)> GetActiveAsync(
            Guid tenantId,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            var query = _context.Branches.AsNoTracking()
                .Where(branch => branch.TenantId == tenantId && branch.IsActive);
            var totalCount = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(branch => branch.IsMainBranch)
                .ThenBy(branch => branch.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return (items, totalCount);
        }

        public Task<bool> IsActiveAsync(Guid tenantId, Guid branchId, CancellationToken ct)
            => _context.Branches.AsNoTracking()
                .AnyAsync(branch => branch.TenantId == tenantId && branch.Id == branchId && branch.IsActive, ct);

        public Task<List<PaymentMethod>> GetPaymentMethodsAsync(
            Guid tenantId,
            Guid branchId,
            CancellationToken ct)
            => _context.BranchPaymentMethods.AsNoTracking()
                .Where(config => config.TenantId == tenantId && config.BranchId == branchId &&
                    config.IsEnabled && config.PaymentMethod.IsActive)
                .OrderBy(config => config.PaymentMethod.DisplayOrder)
                .ThenBy(config => config.PaymentMethod.NameEn)
                .Select(config => config.PaymentMethod)
                .ToListAsync(ct);
    }
}
