using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Modules.Marketing.Jobs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <inheritdoc cref="ISegmentService"/>
    public class SegmentService : ISegmentService
    {
        private const int MaxPageSize = 100;

        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenant;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMarketingAuditLogger _audit;
        private readonly ISegmentComputationEngine _engine;

        public SegmentService(
            PosDbContext context,
            ITenantResolver tenant,
            ICurrentUserAccessor currentUser,
            IMarketingAuditLogger audit,
            ISegmentComputationEngine engine)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public async Task<List<SegmentDto>> GetAllAsync(bool includeInactive, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var segments = await _context.CustomerSegments
                .Include(s => s.Rules)
                .Where(s => s.TenantId == tenantId && (includeInactive || s.IsActive))
                .OrderBy(s => s.Name)
                .ToListAsync(ct);

            var counts = await _context.SegmentMembers
                .Where(m => m.TenantId == tenantId)
                .GroupBy(m => m.SegmentId)
                .Select(g => new { SegmentId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SegmentId, x => x.Count, ct);

            return segments.Select(s => MapToDto(s, counts.GetValueOrDefault(s.Id))).ToList();
        }

        public async Task<SegmentDto> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var segment = await LoadAsync(id, ct);
            var count = await _context.SegmentMembers.CountAsync(m => m.SegmentId == id, ct);
            return MapToDto(segment, count);
        }

        public async Task<SegmentDto> CreateAsync(SegmentUpsertDto dto, CancellationToken ct)
        {
            ValidateRules(dto);
            var tenantId = _tenant.GetTenantId();
            var segment = new CustomerSegment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = dto.Name,
                NameAr = dto.NameAr,
                Type = dto.Type,
                IsActive = dto.IsActive,
                Rules = dto.Rules.Select(r => BuildRule(r, tenantId)).ToList(),
                CreatedByUserId = _currentUser.UserIdOrNull
            };
            _context.CustomerSegments.Add(segment);

            _audit.Track(tenantId, null, MarketingAuditAction.Created,
                nameof(CustomerSegment), segment.Id, null, $"name={segment.Name};type={segment.Type}");

            await _context.SaveChangesAsync(ct);
            return MapToDto(segment, 0);
        }

        public async Task<SegmentDto> UpdateAsync(Guid id, SegmentUpsertDto dto, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();

            ValidateRules(dto);

            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var segment = await LoadAsync(id, ct);
            segment.Name = dto.Name;
            segment.NameAr = dto.NameAr;
            segment.Type = dto.Type;
            segment.IsActive = dto.IsActive;
            segment.UpdatedByUserId = _currentUser.UserIdOrNull;

            _context.SegmentRules.RemoveRange(segment.Rules);
            foreach (var r in dto.Rules)
                _context.SegmentRules.Add(BuildRule(r, tenantId, segment.Id));

            _audit.Track(tenantId, segment.BranchId, MarketingAuditAction.Updated,
                nameof(CustomerSegment), segment.Id, null, $"name={segment.Name};type={segment.Type}");

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var count = await _context.SegmentMembers.CountAsync(m => m.SegmentId == id, ct);
            return MapToDto(await LoadAsync(id, ct), count);
        }

        public async Task DeactivateAsync(Guid id, CancellationToken ct)
        {
            var segment = await LoadAsync(id, ct);
            segment.IsActive = false;
            segment.UpdatedByUserId = _currentUser.UserIdOrNull;

            _audit.Track(segment.TenantId, segment.BranchId, MarketingAuditAction.Deleted,
                nameof(CustomerSegment), segment.Id, null, $"name={segment.Name}");

            await _context.SaveChangesAsync(ct);
        }

        public async Task<PaginatedResponse<SegmentMemberDto>> GetMembersAsync(
            Guid id, int page, int pageSize, bool isArabic, CancellationToken ct)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > MaxPageSize ? 20 : pageSize;
            var tenantId = _tenant.GetTenantId();

            var query =
                from m in _context.SegmentMembers.Where(m => m.SegmentId == id && m.TenantId == tenantId)
                join c in _context.Customers on m.CustomerId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                orderby m.AddedAt descending
                select new { m, c };

            var total = await query.CountAsync(ct);
            var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

            return new PaginatedResponse<SegmentMemberDto>
            {
                Items = rows.Select(x => new SegmentMemberDto
                {
                    CustomerId = x.m.CustomerId,
                    Name = x.c == null ? "—" : (isArabic ? x.c.NameAr : x.c.Name) ?? x.c.Name,
                    PhoneNumber = x.c?.PhoneNumber,
                    AddedAt = x.m.AddedAt,
                    Source = x.m.Source
                }).ToList(),
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task AddMemberAsync(Guid id, Guid customerId, CancellationToken ct)
        {
            var segment = await LoadAsync(id, ct);

            var exists = await _context.SegmentMembers
                .AnyAsync(m => m.SegmentId == id && m.CustomerId == customerId, ct);
            if (exists) return; // idempotent

            var customerExists = await _context.Customers.AnyAsync(c => c.Id == customerId, ct);
            if (!customerExists) throw new NotFoundException("Customer was not found.");

            _context.SegmentMembers.Add(new SegmentMember
            {
                Id = Guid.NewGuid(),
                TenantId = segment.TenantId,
                BranchId = segment.BranchId,
                SegmentId = id,
                CustomerId = customerId,
                AddedAt = DateTime.UtcNow,
                Source = SegmentMemberSource.Manual,
                CreatedByUserId = _currentUser.UserIdOrNull
            });

            _audit.Track(segment.TenantId, segment.BranchId, MarketingAuditAction.Updated,
                nameof(SegmentMember), id, customerId, "segment-member-added;source=Manual");

            await _context.SaveChangesAsync(ct);
        }

        public async Task RemoveMemberAsync(Guid id, Guid customerId, CancellationToken ct)
        {
            var segment = await LoadAsync(id, ct);
            var member = await _context.SegmentMembers
                .FirstOrDefaultAsync(m => m.SegmentId == id && m.CustomerId == customerId, ct);
            if (member is null) return;

            _context.SegmentMembers.Remove(member);
            _audit.Track(segment.TenantId, segment.BranchId, MarketingAuditAction.Updated,
                nameof(SegmentMember), id, customerId, "segment-member-removed");

            await _context.SaveChangesAsync(ct);
        }

        public async Task<SegmentComputeResultDto> ComputeAsync(Guid id, CancellationToken ct)
        {
            var segment = await LoadAsync(id, ct);
            if (segment.Type != CustomerSegmentType.Dynamic)
                throw new ValidationException("Only dynamic segments can be computed.");
            return await _engine.ComputeAsync(segment, ct);
        }

        private async Task<CustomerSegment> LoadAsync(Guid id, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            return await _context.CustomerSegments
                .Include(s => s.Rules)
                .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Segment was not found.");
        }

        private static void ValidateRules(SegmentUpsertDto dto)
        {
            var unsupported = dto.Rules
                .Where(r => !SegmentEvaluator.IsSupported(r.Field))
                .Select(r => r.Field.ToString())
                .Distinct()
                .ToList();
            if (unsupported.Count > 0)
                throw new ValidationException($"Unsupported segment field(s): {string.Join(", ", unsupported)}.");
        }

        private SegmentRule BuildRule(SegmentRuleUpsertDto dto, Guid tenantId, Guid? segmentId = null) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SegmentId = segmentId ?? Guid.Empty,
            Field = dto.Field,
            Operator = dto.Operator,
            Value = dto.Value,
            LogicGroup = dto.LogicGroup,
            CreatedByUserId = _currentUser.UserIdOrNull
        };

        private static SegmentDto MapToDto(CustomerSegment s, int memberCount) => new()
        {
            Id = s.Id,
            Name = s.Name,
            NameAr = s.NameAr,
            Type = s.Type,
            IsActive = s.IsActive,
            LastComputedAt = s.LastComputedAt,
            MemberCount = memberCount,
            Rules = s.Rules
                .Select(r => new SegmentRuleDto
                {
                    Id = r.Id,
                    Field = r.Field,
                    Operator = r.Operator,
                    Value = r.Value,
                    LogicGroup = r.LogicGroup
                })
                .ToList()
        };
    }
}
