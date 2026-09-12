using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Repositories
{
    /// <summary>
    /// Settings repository implementation using EF Core.
    /// </summary>
    public class SettingsRepository : ISettingsRepository
    {
        private readonly PosDbContext _context;

        public SettingsRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<SystemSettings?> GetSettingsAsync(
            Guid? tenantId,
            CancellationToken cancellationToken = default)
        {
            var settings = _context.SystemSettings.AsNoTracking();
            if (!tenantId.HasValue)
                return await settings.FirstOrDefaultAsync(s => s.TenantId == null, cancellationToken);

            return await settings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
                ?? await settings.FirstOrDefaultAsync(s => s.TenantId == null, cancellationToken);
        }

        public async Task<SystemSettings> SaveSettingsAsync(
            SystemSettings settings,
            Guid? tenantId,
            CancellationToken cancellationToken = default)
        {
            var targetTenantId = tenantId ?? settings.TenantId;
            var existing = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.TenantId == targetTenantId, cancellationToken);

            if (existing == null)
            {
                existing = new SystemSettings();
                _context.Entry(existing).CurrentValues.SetValues(settings);
                existing.Id = Guid.NewGuid();
                existing.TenantId = targetTenantId;
                _context.SystemSettings.Add(existing);
            }
            else
            {
                var existingId = existing.Id;
                _context.Entry(existing).CurrentValues.SetValues(settings);
                existing.Id = existingId;
                existing.TenantId = targetTenantId;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return existing;
        }
    }
}
