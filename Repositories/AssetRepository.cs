using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed class AssetRepository : IAssetRepository
    {
        private readonly PosDbContext _context;

        public AssetRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<int> CountCreatedOnDateAsync(DateTime dayUtc, CancellationToken ct)
        {
            var start = dayUtc.Date;
            var end = start.AddDays(1);
            return _context.Assets.AsNoTracking().CountAsync(a => a.CreatedAt >= start && a.CreatedAt < end, ct);
        }

        public Task<bool> AssetCodeExistsAsync(string assetCode, Guid? excludeId, CancellationToken ct)
            => _context.Assets.AsNoTracking()
                .AnyAsync(a => a.AssetCode == assetCode && (excludeId == null || a.Id != excludeId.Value), ct);

        public Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken ct)
            => _context.AssetCategories.AsNoTracking().AnyAsync(c => c.Id == categoryId && c.IsActive, ct);

        public Task<bool> StaffProfileExistsAsync(Guid staffProfileId, CancellationToken ct)
            => _context.StaffProfiles.AsNoTracking().AnyAsync(s => s.Id == staffProfileId, ct);

        public Task<bool> CanUserAccessAssetAsync(Guid assetId, Guid userId, CancellationToken ct)
            => _context.Assets.AsNoTracking()
                .AnyAsync(a => a.Id == assetId
                    && a.AssignedToEmployeeId != null
                    && a.AssignedToEmployee != null
                    && a.AssignedToEmployee.UserId == userId, ct);

        public Task AddAssetAsync(Asset asset, CancellationToken ct)
            => _context.Assets.AddAsync(asset, ct).AsTask();

        public Task<Asset?> GetTrackedAssetAsync(Guid id, Guid branchId, CancellationToken ct)
            => _context.Assets
                .Include(a => a.Attachments)
                .Include(a => a.MaintenanceRecords)
                .FirstOrDefaultAsync(a => a.Id == id && a.BranchId == branchId, ct);

        public Task<Asset?> GetTrackedAssetIgnoringFiltersAsync(Guid id, Guid branchId, CancellationToken ct)
            => _context.Assets
                .IgnoreQueryFilters()
                .Include(a => a.Attachments)
                .Include(a => a.MaintenanceRecords)
                .FirstOrDefaultAsync(a => a.Id == id && a.BranchId == branchId, ct);

        public async Task<AssetDetailsDto?> GetDetailsAsync(
            Guid id,
            Guid branchId,
            bool isArabic,
            string downloadRouteBase,
            Func<string, string?> publicUrlBuilder,
            CancellationToken ct)
        {
            var dto = await _context.Assets
                .AsNoTracking()
                .Where(a => a.Id == id && a.BranchId == branchId)
                .Select(a => new AssetDetailsDto
                {
                    Id = a.Id,
                    AssetCode = a.AssetCode,
                    AssetName = isArabic ? a.AssetNameAr : a.AssetNameEn,
                    AssetNameEn = a.AssetNameEn,
                    AssetNameAr = a.AssetNameAr,
                    DescriptionEn = a.DescriptionEn,
                    DescriptionAr = a.DescriptionAr,
                    AssetCategoryId = a.AssetCategoryId,
                    CategoryName = isArabic ? a.AssetCategory.NameAr : a.AssetCategory.NameEn,
                    CategoryColor = a.AssetCategory.Color,
                    CategoryIcon = a.AssetCategory.Icon,
                    AssetType = a.AssetType,
                    Brand = a.Brand,
                    Model = a.Model,
                    SerialNumber = a.SerialNumber,
                    Barcode = a.Barcode,
                    QRCode = a.QRCode,
                    PurchaseDate = a.PurchaseDate,
                    PurchaseCost = a.PurchaseCost,
                    CurrentValue = a.CurrentValue,
                    WarrantyStartDate = a.WarrantyStartDate,
                    WarrantyEndDate = a.WarrantyEndDate,
                    SupplierName = a.SupplierName,
                    Status = a.Status.ToString(),
                    Condition = a.Condition.ToString(),
                    AssignedToEmployeeId = a.AssignedToEmployeeId,
                    AssignedToEmployeeName = a.AssignedToEmployee != null && a.AssignedToEmployee.User != null
                        ? (isArabic && a.AssignedToEmployee.User.FullNameAr != null && a.AssignedToEmployee.User.FullNameAr != string.Empty
                            ? a.AssignedToEmployee.User.FullNameAr
                            : (a.AssignedToEmployee.User.FullName ?? a.AssignedToEmployee.User.Username))
                        : null,
                    AssignedLocation = a.AssignedLocation,
                    Department = a.Department,
                    Notes = a.Notes,
                    IsActive = a.IsActive,
                    AttachmentsCount = a.Attachments.Count,
                    MaintenanceCount = a.MaintenanceRecords.Count,
                    CreatedById = a.CreatedById,
                    CreatedBy = a.CreatedBy,
                    UpdatedById = a.UpdatedById,
                    UpdatedBy = a.UpdatedBy,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                    Attachments = a.Attachments
                        .OrderByDescending(x => x.UploadedAt)
                        .Select(x => new AssetAttachmentDto
                        {
                            Id = x.Id,
                            AssetId = x.AssetId,
                            MaintenanceRecordId = x.MaintenanceRecordId,
                            FileName = x.FileName,
                            OriginalFileName = x.OriginalFileName,
                            FileExtension = x.FileExtension,
                            FileSize = x.FileSize,
                            MimeType = x.MimeType,
                            FilePath = x.FilePath,
                            FileUrl = x.FilePath,
                            DownloadUrl = x.Id.ToString(),
                            AttachmentType = x.AttachmentType.ToString(),
                            UploadedAt = x.UploadedAt,
                            UploadedById = x.UploadedById,
                            UploadedBy = x.UploadedBy
                        })
                        .ToList(),
                    MaintenanceHistory = a.MaintenanceRecords
                        .OrderByDescending(x => x.MaintenanceDate)
                        .Take(25)
                        .Select(x => new AssetMaintenanceRecordDto
                        {
                            Id = x.Id,
                            AssetId = x.AssetId,
                            AssetCode = a.AssetCode,
                            AssetName = isArabic ? a.AssetNameAr : a.AssetNameEn,
                            MaintenanceType = x.MaintenanceType,
                            Description = x.Description,
                            Cost = x.Cost,
                            PerformedBy = x.PerformedBy,
                            Vendor = x.Vendor,
                            MaintenanceDate = x.MaintenanceDate,
                            NextMaintenanceDate = x.NextMaintenanceDate,
                            Status = x.Status.ToString(),
                            Notes = x.Notes,
                            CreatedBy = x.CreatedBy,
                            CreatedAt = x.CreatedAt,
                            AttachmentsCount = x.Attachments.Count
                        })
                        .ToList(),
                    ActivityTimeline = a.ActivityLogs
                        .OrderByDescending(x => x.Timestamp)
                        .Take(50)
                        .Select(x => new AssetActivityLogDto
                        {
                            Id = x.Id,
                            AssetId = x.AssetId,
                            AssetCode = a.AssetCode,
                            AssetName = isArabic ? a.AssetNameAr : a.AssetNameEn,
                            ActionType = x.ActionType.ToString(),
                            OldValue = x.OldValue,
                            NewValue = x.NewValue,
                            PerformedById = x.PerformedById,
                            PerformedBy = x.PerformedBy,
                            Timestamp = x.Timestamp,
                            IpAddress = x.IpAddress,
                            DeviceInfo = x.DeviceInfo
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(ct);

            if (dto is null) return null;

            foreach (var attachment in dto.Attachments)
            {
                attachment.FileUrl = publicUrlBuilder(attachment.FilePath) ?? string.Empty;
                attachment.DownloadUrl = $"{downloadRouteBase}/{dto.Id}/attachments/{attachment.Id}/download";
            }

            return dto;
        }

        public async Task<PaginatedResponse<AssetListItemDto>> GetPagedAsync(
            AssetQueryDto query,
            Guid branchId,
            bool isArabic,
            Guid? assignedUserId,
            CancellationToken ct)
        {
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);
            var rows = ApplyAssetFilters(_context.Assets.AsNoTracking().Where(a => a.BranchId == branchId), query, assignedUserId);
            var total = await rows.CountAsync(ct);
            var items = await ProjectAssetList(ApplyAssetSort(rows, query), isArabic)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PaginatedResponse<AssetListItemDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public Task<List<AssetListItemDto>> GetExportRowsAsync(
            AssetQueryDto query,
            Guid branchId,
            bool isArabic,
            Guid? assignedUserId,
            int maxRows,
            CancellationToken ct)
            => ProjectAssetList(
                    ApplyAssetSort(ApplyAssetFilters(_context.Assets.AsNoTracking().Where(a => a.BranchId == branchId), query, assignedUserId), query),
                    isArabic)
                .Take(Math.Clamp(maxRows, 1, 10000))
                .ToListAsync(ct);

        public async Task<AssetDashboardDto> GetDashboardAsync(Guid branchId, bool isArabic, Guid? assignedUserId, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var warrantyWindowEnd = now.AddDays(30);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var trendStart = monthStart.AddMonths(-5);

            var assets = RestrictToAssignedUser(_context.Assets.AsNoTracking().Where(a => a.BranchId == branchId), assignedUserId);
            var maintenance = RestrictMaintenanceToAssignedUser(
                _context.AssetMaintenanceRecords.AsNoTracking().Where(m => m.Asset.BranchId == branchId),
                assignedUserId);

            var byCategory = await assets
                .GroupBy(a => new
                {
                    Label = isArabic ? a.AssetCategory.NameAr : a.AssetCategory.NameEn,
                    a.AssetCategory.Color
                })
                .Select(g => new AssetChartPointDto
                {
                    Label = g.Key.Label,
                    Color = g.Key.Color,
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value)
                .ToListAsync(ct);

            var statusRows = await assets
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var maintenanceTrendRows = await maintenance
                .Where(m => m.MaintenanceDate >= trendStart)
                .GroupBy(m => new { m.MaintenanceDate.Year, m.MaintenanceDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Cost = g.Sum(x => x.Cost) })
                .ToListAsync(ct);

            var assetCostTrendRows = await assets
                .Where(a => a.PurchaseDate != null && a.PurchaseDate >= trendStart)
                .GroupBy(a => new { a.PurchaseDate!.Value.Year, a.PurchaseDate!.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Cost = g.Sum(x => x.PurchaseCost ?? 0m) })
                .ToListAsync(ct);

            return new AssetDashboardDto
            {
                TotalAssets = await assets.CountAsync(ct),
                ActiveAssets = await assets.CountAsync(a => a.Status == AssetStatus.Active, ct),
                AssetsInMaintenance = await assets.CountAsync(a => a.Status == AssetStatus.InMaintenance, ct),
                DamagedAssets = await assets.CountAsync(a => a.Status == AssetStatus.Damaged || a.Condition == AssetCondition.Damaged || a.Condition == AssetCondition.Critical, ct),
                ExpiringWarranties = await assets.CountAsync(a => a.WarrantyEndDate != null && a.WarrantyEndDate >= now && a.WarrantyEndDate <= warrantyWindowEnd, ct),
                AssetValue = await assets.SumAsync(a => (decimal?)a.CurrentValue, ct) ?? 0m,
                MaintenanceCostThisMonth = await maintenance.Where(m => m.MaintenanceDate >= monthStart).SumAsync(m => (decimal?)m.Cost, ct) ?? 0m,
                AssetsByCategory = byCategory,
                AssetsByStatus = statusRows.Select(x => new AssetChartPointDto { Label = x.Status.ToString(), Value = x.Count }).ToList(),
                MaintenanceTrend = BuildMonthlyTrend(maintenanceTrendRows.Select(x => (x.Year, x.Month, x.Cost)), monthStart),
                AssetCostTrend = BuildMonthlyTrend(assetCostTrendRows.Select(x => (x.Year, x.Month, x.Cost)), monthStart)
            };
        }

        public async Task<List<AssetCategoryDto>> GetCategoriesAsync(bool activeOnly, bool isArabic, CancellationToken ct)
        {
            var rows = _context.AssetCategories.AsNoTracking();
            if (activeOnly) rows = rows.Where(c => c.IsActive);

            return await rows
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.NameEn)
                .Select(c => new AssetCategoryDto
                {
                    Id = c.Id,
                    NameEn = c.NameEn,
                    NameAr = c.NameAr,
                    DisplayName = isArabic ? c.NameAr : c.NameEn,
                    Icon = c.Icon,
                    Color = c.Color,
                    IsActive = c.IsActive,
                    SortOrder = c.SortOrder,
                    AssetCount = c.Assets.Count
                })
                .ToListAsync(ct);
        }

        public Task<AssetCategory?> GetTrackedCategoryAsync(Guid id, CancellationToken ct)
            => _context.AssetCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task AddCategoryAsync(AssetCategory category, CancellationToken ct)
            => _context.AssetCategories.AddAsync(category, ct).AsTask();

        public Task<bool> CategoryNameExistsAsync(string nameEn, string nameAr, Guid? excludeId, CancellationToken ct)
            => _context.AssetCategories.AsNoTracking().AnyAsync(c =>
                (EF.Functions.ILike(c.NameEn, nameEn) || EF.Functions.ILike(c.NameAr, nameAr))
                && (excludeId == null || c.Id != excludeId.Value), ct);

        public Task<bool> CategoryInUseAsync(Guid id, CancellationToken ct)
            => _context.Assets.AsNoTracking().AnyAsync(a => a.AssetCategoryId == id, ct);

        public Task<bool> HasAnyCategoryAsync(CancellationToken ct)
            => _context.AssetCategories.AsNoTracking().AnyAsync(ct);

        public Task AddAttachmentAsync(AssetAttachment attachment, CancellationToken ct)
            => _context.AssetAttachments.AddAsync(attachment, ct).AsTask();

        public Task<AssetAttachment?> GetAttachmentAsync(Guid assetId, Guid attachmentId, CancellationToken ct)
            => _context.AssetAttachments.FirstOrDefaultAsync(a => a.AssetId == assetId && a.Id == attachmentId, ct);

        public Task RemoveAttachmentAsync(AssetAttachment attachment, CancellationToken ct)
        {
            _context.AssetAttachments.Remove(attachment);
            return Task.CompletedTask;
        }

        public Task AddMaintenanceAsync(AssetMaintenanceRecord maintenance, CancellationToken ct)
            => _context.AssetMaintenanceRecords.AddAsync(maintenance, ct).AsTask();

        public async Task<PaginatedResponse<AssetMaintenanceRecordDto>> GetMaintenancePagedAsync(
            AssetMaintenanceQueryDto query,
            Guid branchId,
            bool isArabic,
            Guid? assignedUserId,
            CancellationToken ct)
        {
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);
            var rows = ApplyMaintenanceFilters(
                RestrictMaintenanceToAssignedUser(
                    _context.AssetMaintenanceRecords.AsNoTracking().Where(m => m.Asset.BranchId == branchId),
                    assignedUserId),
                query);
            var total = await rows.CountAsync(ct);
            var items = await ProjectMaintenance(rows.OrderByDescending(m => m.MaintenanceDate), isArabic)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PaginatedResponse<AssetMaintenanceRecordDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public Task<List<AssetMaintenanceRecordDto>> GetMaintenanceForAssetAsync(Guid assetId, bool isArabic, CancellationToken ct)
            => ProjectMaintenance(
                    _context.AssetMaintenanceRecords.AsNoTracking()
                        .Where(m => m.AssetId == assetId)
                        .OrderByDescending(m => m.MaintenanceDate),
                    isArabic)
                .ToListAsync(ct);

        public Task AddLogAsync(AssetActivityLog log, CancellationToken ct)
            => _context.AssetActivityLogs.AddAsync(log, ct).AsTask();

        public async Task<PaginatedResponse<AssetActivityLogDto>> GetLogsPagedAsync(
            AssetActivityLogQueryDto query,
            Guid branchId,
            bool isArabic,
            Guid? assignedUserId,
            CancellationToken ct)
        {
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);
            var rows = ApplyLogFilters(
                RestrictLogsToAssignedUser(
                    _context.AssetActivityLogs.AsNoTracking().Where(l => l.Asset.BranchId == branchId),
                    assignedUserId),
                query);
            var total = await rows.CountAsync(ct);
            var items = await ProjectLogs(rows.OrderByDescending(l => l.Timestamp), isArabic)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PaginatedResponse<AssetActivityLogDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public Task<int> SaveChangesAsync(CancellationToken ct)
            => _context.SaveChangesAsync(ct);

        private static IQueryable<Asset> ApplyAssetFilters(IQueryable<Asset> rows, AssetQueryDto query, Guid? assignedUserId)
        {
            rows = RestrictToAssignedUser(rows, assignedUserId);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                rows = rows.Where(a =>
                    EF.Functions.ILike(a.AssetCode, $"%{term}%")
                    || EF.Functions.ILike(a.AssetNameEn, $"%{term}%")
                    || EF.Functions.ILike(a.AssetNameAr, $"%{term}%")
                    || (a.SerialNumber != null && EF.Functions.ILike(a.SerialNumber, $"%{term}%"))
                    || (a.Barcode != null && EF.Functions.ILike(a.Barcode, $"%{term}%"))
                    || (a.Brand != null && EF.Functions.ILike(a.Brand, $"%{term}%"))
                    || (a.Model != null && EF.Functions.ILike(a.Model, $"%{term}%")));
            }

            if (query.CategoryId.HasValue)
                rows = rows.Where(a => a.AssetCategoryId == query.CategoryId.Value);

            if (!string.IsNullOrWhiteSpace(query.Status)
                && Enum.TryParse<AssetStatus>(query.Status, true, out var status))
                rows = rows.Where(a => a.Status == status);

            if (!string.IsNullOrWhiteSpace(query.Condition)
                && Enum.TryParse<AssetCondition>(query.Condition, true, out var condition))
                rows = rows.Where(a => a.Condition == condition);

            if (query.AssignedEmployeeId.HasValue)
                rows = rows.Where(a => a.AssignedToEmployeeId == query.AssignedEmployeeId.Value);

            if (query.DateFrom.HasValue)
                rows = rows.Where(a => a.PurchaseDate >= query.DateFrom.Value.Date);

            if (query.DateTo.HasValue)
                rows = rows.Where(a => a.PurchaseDate < query.DateTo.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(query.MaintenanceStatus)
                && Enum.TryParse<AssetMaintenanceStatus>(query.MaintenanceStatus, true, out var maintenanceStatus))
                rows = rows.Where(a => a.MaintenanceRecords.Any(m => m.Status == maintenanceStatus));

            rows = ApplyWarrantyFilter(rows, query.WarrantyExpiry);
            return rows;
        }

        private static IQueryable<Asset> ApplyWarrantyFilter(IQueryable<Asset> rows, string? warrantyExpiry)
        {
            if (string.IsNullOrWhiteSpace(warrantyExpiry)) return rows;

            var now = DateTime.UtcNow.Date;
            var key = warrantyExpiry.Trim().ToLowerInvariant();
            if (key == "expired")
                return rows.Where(a => a.WarrantyEndDate != null && a.WarrantyEndDate < now);
            if (key == "none")
                return rows.Where(a => a.WarrantyEndDate == null);
            if (int.TryParse(key, out var days) && days > 0)
            {
                var end = now.AddDays(Math.Min(days, 365));
                return rows.Where(a => a.WarrantyEndDate != null && a.WarrantyEndDate >= now && a.WarrantyEndDate <= end);
            }

            return rows;
        }

        private static IQueryable<Asset> ApplyAssetSort(IQueryable<Asset> rows, AssetQueryDto query)
        {
            var desc = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            return (query.SortBy ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "assetcode" => desc ? rows.OrderByDescending(a => a.AssetCode) : rows.OrderBy(a => a.AssetCode),
                "name" => desc ? rows.OrderByDescending(a => a.AssetNameEn) : rows.OrderBy(a => a.AssetNameEn),
                "status" => desc ? rows.OrderByDescending(a => a.Status) : rows.OrderBy(a => a.Status),
                "condition" => desc ? rows.OrderByDescending(a => a.Condition) : rows.OrderBy(a => a.Condition),
                "purchasecost" => desc ? rows.OrderByDescending(a => a.PurchaseCost) : rows.OrderBy(a => a.PurchaseCost),
                "currentvalue" => desc ? rows.OrderByDescending(a => a.CurrentValue) : rows.OrderBy(a => a.CurrentValue),
                "warrantyenddate" => desc ? rows.OrderByDescending(a => a.WarrantyEndDate) : rows.OrderBy(a => a.WarrantyEndDate),
                _ => rows.OrderByDescending(a => a.CreatedAt)
            };
        }

        private static IQueryable<AssetListItemDto> ProjectAssetList(IQueryable<Asset> rows, bool isArabic)
            => rows.Select(a => new AssetListItemDto
            {
                Id = a.Id,
                AssetCode = a.AssetCode,
                AssetName = isArabic ? a.AssetNameAr : a.AssetNameEn,
                AssetNameEn = a.AssetNameEn,
                AssetNameAr = a.AssetNameAr,
                AssetCategoryId = a.AssetCategoryId,
                CategoryName = isArabic ? a.AssetCategory.NameAr : a.AssetCategory.NameEn,
                CategoryColor = a.AssetCategory.Color,
                CategoryIcon = a.AssetCategory.Icon,
                AssetType = a.AssetType,
                Brand = a.Brand,
                Model = a.Model,
                SerialNumber = a.SerialNumber,
                Barcode = a.Barcode,
                QRCode = a.QRCode,
                PurchaseCost = a.PurchaseCost,
                CurrentValue = a.CurrentValue,
                WarrantyEndDate = a.WarrantyEndDate,
                Status = a.Status.ToString(),
                Condition = a.Condition.ToString(),
                AssignedToEmployeeId = a.AssignedToEmployeeId,
                AssignedToEmployeeName = a.AssignedToEmployee != null && a.AssignedToEmployee.User != null
                    ? (isArabic && a.AssignedToEmployee.User.FullNameAr != null && a.AssignedToEmployee.User.FullNameAr != string.Empty
                        ? a.AssignedToEmployee.User.FullNameAr
                        : (a.AssignedToEmployee.User.FullName ?? a.AssignedToEmployee.User.Username))
                    : null,
                AssignedLocation = a.AssignedLocation,
                Department = a.Department,
                IsActive = a.IsActive,
                AttachmentsCount = a.Attachments.Count,
                MaintenanceCount = a.MaintenanceRecords.Count,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            });

        private static IQueryable<AssetMaintenanceRecord> ApplyMaintenanceFilters(
            IQueryable<AssetMaintenanceRecord> rows,
            AssetMaintenanceQueryDto query)
        {
            if (query.AssetId.HasValue)
                rows = rows.Where(m => m.AssetId == query.AssetId.Value);

            if (!string.IsNullOrWhiteSpace(query.Status)
                && Enum.TryParse<AssetMaintenanceStatus>(query.Status, true, out var status))
                rows = rows.Where(m => m.Status == status);

            if (query.DateFrom.HasValue)
                rows = rows.Where(m => m.MaintenanceDate >= query.DateFrom.Value.Date);

            if (query.DateTo.HasValue)
                rows = rows.Where(m => m.MaintenanceDate < query.DateTo.Value.Date.AddDays(1));

            if (query.DueOnly == true)
            {
                var now = DateTime.UtcNow.Date;
                rows = rows.Where(m => m.NextMaintenanceDate != null && m.NextMaintenanceDate <= now);
            }

            return rows;
        }

        private static IQueryable<AssetMaintenanceRecordDto> ProjectMaintenance(IQueryable<AssetMaintenanceRecord> rows, bool isArabic)
            => rows.Select(m => new AssetMaintenanceRecordDto
            {
                Id = m.Id,
                AssetId = m.AssetId,
                AssetCode = m.Asset.AssetCode,
                AssetName = isArabic ? m.Asset.AssetNameAr : m.Asset.AssetNameEn,
                MaintenanceType = m.MaintenanceType,
                Description = m.Description,
                Cost = m.Cost,
                PerformedBy = m.PerformedBy,
                Vendor = m.Vendor,
                MaintenanceDate = m.MaintenanceDate,
                NextMaintenanceDate = m.NextMaintenanceDate,
                Status = m.Status.ToString(),
                Notes = m.Notes,
                CreatedBy = m.CreatedBy,
                CreatedAt = m.CreatedAt,
                AttachmentsCount = m.Attachments.Count
            });

        private static IQueryable<AssetActivityLog> ApplyLogFilters(IQueryable<AssetActivityLog> rows, AssetActivityLogQueryDto query)
        {
            if (query.AssetId.HasValue)
                rows = rows.Where(l => l.AssetId == query.AssetId.Value);

            if (!string.IsNullOrWhiteSpace(query.ActionType)
                && Enum.TryParse<AssetActivityActionType>(query.ActionType, true, out var action))
                rows = rows.Where(l => l.ActionType == action);

            if (query.DateFrom.HasValue)
                rows = rows.Where(l => l.Timestamp >= query.DateFrom.Value.Date);

            if (query.DateTo.HasValue)
                rows = rows.Where(l => l.Timestamp < query.DateTo.Value.Date.AddDays(1));

            return rows;
        }

        private static IQueryable<AssetActivityLogDto> ProjectLogs(IQueryable<AssetActivityLog> rows, bool isArabic)
            => rows.Select(l => new AssetActivityLogDto
            {
                Id = l.Id,
                AssetId = l.AssetId,
                AssetCode = l.Asset.AssetCode,
                AssetName = isArabic ? l.Asset.AssetNameAr : l.Asset.AssetNameEn,
                ActionType = l.ActionType.ToString(),
                OldValue = l.OldValue,
                NewValue = l.NewValue,
                PerformedById = l.PerformedById,
                PerformedBy = l.PerformedBy,
                Timestamp = l.Timestamp,
                IpAddress = l.IpAddress,
                DeviceInfo = l.DeviceInfo
            });

        private static IQueryable<Asset> RestrictToAssignedUser(IQueryable<Asset> rows, Guid? assignedUserId)
            => assignedUserId.HasValue
                ? rows.Where(a => a.AssignedToEmployeeId != null
                    && a.AssignedToEmployee != null
                    && a.AssignedToEmployee.UserId == assignedUserId.Value)
                : rows;

        private static IQueryable<AssetMaintenanceRecord> RestrictMaintenanceToAssignedUser(
            IQueryable<AssetMaintenanceRecord> rows,
            Guid? assignedUserId)
            => assignedUserId.HasValue
                ? rows.Where(m => m.Asset.AssignedToEmployeeId != null
                    && m.Asset.AssignedToEmployee != null
                    && m.Asset.AssignedToEmployee.UserId == assignedUserId.Value)
                : rows;

        private static IQueryable<AssetActivityLog> RestrictLogsToAssignedUser(
            IQueryable<AssetActivityLog> rows,
            Guid? assignedUserId)
            => assignedUserId.HasValue
                ? rows.Where(l => l.Asset.AssignedToEmployeeId != null
                    && l.Asset.AssignedToEmployee != null
                    && l.Asset.AssignedToEmployee.UserId == assignedUserId.Value)
                : rows;

        private static List<AssetTrendPointDto> BuildMonthlyTrend(IEnumerable<(int Year, int Month, decimal Cost)> rows, DateTime monthStart)
        {
            var lookup = rows.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Cost);
            var result = new List<AssetTrendPointDto>();
            for (var i = 5; i >= 0; i--)
            {
                var month = monthStart.AddMonths(-i);
                var key = $"{month.Year:D4}-{month.Month:D2}";
                result.Add(new AssetTrendPointDto
                {
                    Period = key,
                    Value = lookup.TryGetValue(key, out var value) ? value : 0m
                });
            }

            return result;
        }
    }
}
