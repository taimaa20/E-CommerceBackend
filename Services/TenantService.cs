using RestaurantPos.Api.Data;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace RestaurantPos.Api.Services
{
    public interface ITenantService
    {
        Task<Tenant> CreateTenantAsync(string name, string domain, string ownerEmail, string ownerPassword);
        Task<Tenant?> GetTenantByDomainAsync(string domain);
        Task<List<Tenant>> GetAllTenantsAsync();
    }

    public class TenantService : ITenantService
    {
        private readonly PosDbContext _context;
        private readonly ILogger<TenantService> _logger;

        public TenantService(PosDbContext context, ILogger<TenantService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Tenant> CreateTenantAsync(string name, string domain, string ownerEmail, string ownerPassword)
        {
            // 1. Create Tenant
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = name,
                Domain = domain,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"[SaaS] New Tenant Created: {name} (ID: {tenant.Id})");

            // 2. Create Main Branch and Owner User (Admin Role)
            var mainBranch = new Branch
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = BranchDefaults.MainBranchName,
                NameAr = BranchDefaults.MainBranchNameAr,
                Code = BranchDefaults.MainBranchCode,
                IsMainBranch = true,
                IsActive = true
            };

            var ownerUser = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Username = ownerEmail,
                PasswordHash = PasswordHelper.Hash(ownerPassword),
                Role = UserRole.Admin,
                FullName = $"{name} - Owner",
                MonthlySalary = 0,
                CommissionRate = 0
            };

            _context.Branches.Add(mainBranch);
            _context.Users.Add(ownerUser);
            _context.UserBranches.Add(new UserBranch
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = ownerUser.Id,
                BranchId = mainBranch.Id,
                IsDefault = true
            });

            // 3. Create Default Categories (Optional - if you have Category entity)
            // For now, we'll skip this as we don't have a Category table yet
            // But in a real system, you'd seed default categories here

            await _context.SaveChangesAsync();

            _logger.LogInformation($"[SaaS] Tenant {name} onboarding completed. Owner: {ownerEmail}");

            return tenant;
        }

        public async Task<Tenant?> GetTenantByDomainAsync(string domain)
        {
            // Note: Tenants table doesn't have TenantId filter (it's the master table)
            return await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Domain == domain && t.IsActive);
        }

        public async Task<List<Tenant>> GetAllTenantsAsync()
        {
            return await _context.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }
    }
}
