using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class PaymentMethodRepository : IPaymentMethodRepository
    {
        private readonly PosDbContext _context;

        public PaymentMethodRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<List<PaymentMethod>> GetActiveAsync(CancellationToken ct = default)
        {
            return _context.PaymentMethods
                .AsNoTracking()
                .Where(m => m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ThenBy(m => m.NameEn)
                .ToListAsync(ct);
        }

        public Task<List<PaymentMethod>> GetAllAsync(CancellationToken ct = default)
        {
            return _context.PaymentMethods
                .AsNoTracking()
                .OrderBy(m => m.DisplayOrder)
                .ThenBy(m => m.NameEn)
                .ToListAsync(ct);
        }

        public Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return _context.PaymentMethods
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id, ct);
        }

        public async Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct = default)
        {
            var normalized = code.Trim();
            return await _context.PaymentMethods
                .AsNoTracking()
                .AnyAsync(m =>
                    m.Code.ToLower() == normalized.ToLower() &&
                    (!excludeId.HasValue || m.Id != excludeId.Value), ct);
        }

        public async Task ClearDefaultAsync(Guid? excludeId, CancellationToken ct = default)
        {
            await _context.PaymentMethods
                .Where(m => m.IsDefault && (!excludeId.HasValue || m.Id != excludeId.Value))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.IsDefault, false)
                    .SetProperty(m => m.UpdatedAt, DateTime.UtcNow), ct);
        }

        public async Task<PaymentMethod> AddAsync(PaymentMethod method, CancellationToken ct = default)
        {
            _context.PaymentMethods.Add(method);
            await _context.SaveChangesAsync(ct);
            return method;
        }

        public async Task<PaymentMethod> UpdateAsync(PaymentMethod method, CancellationToken ct = default)
        {
            _context.PaymentMethods.Update(method);
            await _context.SaveChangesAsync(ct);
            return method;
        }

        public async Task DeleteAsync(PaymentMethod method, CancellationToken ct = default)
        {
            method.DeletedAt = DateTime.UtcNow;
            method.IsActive = false;
            method.IsDefault = false;
            _context.PaymentMethods.Update(method);
            await _context.SaveChangesAsync(ct);
        }

    }
}
