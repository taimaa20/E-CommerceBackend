using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Services.Assets;

namespace RestaurantPos.Api.Services
{
    public sealed class AssetService : IAssetService
    {
        private const string CodePrefix = "AST";
        private const int CodeSequencePadding = 5;
        private const int ExportMaxRows = 10000;
        private const string ControllerRoute = "/api/assets";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private static readonly IReadOnlyDictionary<AssetStatus, AssetStatus[]> AllowedStatusTransitions =
            new Dictionary<AssetStatus, AssetStatus[]>
            {
                [AssetStatus.Active] = new[] { AssetStatus.InMaintenance, AssetStatus.Damaged, AssetStatus.Lost, AssetStatus.Retired, AssetStatus.InStorage },
                [AssetStatus.InMaintenance] = new[] { AssetStatus.Active, AssetStatus.Damaged, AssetStatus.Retired },
                [AssetStatus.Damaged] = new[] { AssetStatus.InMaintenance, AssetStatus.Retired, AssetStatus.Disposed },
                [AssetStatus.Lost] = new[] { AssetStatus.Active, AssetStatus.Disposed },
                [AssetStatus.Retired] = new[] { AssetStatus.Disposed, AssetStatus.InStorage },
                [AssetStatus.InStorage] = new[] { AssetStatus.Active, AssetStatus.InMaintenance, AssetStatus.Retired, AssetStatus.Lost },
                [AssetStatus.Disposed] = Array.Empty<AssetStatus>()
            };

        private static readonly AssetDefaultCategory[] DefaultCategories =
        {
            new("POS Devices", "أجهزة نقاط البيع", "Monitor", "#2563eb", 10),
            new("Printers", "الطابعات", "Printer", "#16a34a", 20),
            new("Kitchen Equipment", "معدات المطبخ", "ChefHat", "#f97316", 30),
            new("Network Devices", "أجهزة الشبكة", "Router", "#0f766e", 40),
            new("Furniture", "الأثاث", "Armchair", "#7c3aed", 50),
            new("Electronics", "الإلكترونيات", "Laptop", "#0891b2", 60),
            new("Refrigerators", "الثلاجات", "Refrigerator", "#0284c7", 70),
            new("Vehicles", "المركبات", "Truck", "#475569", 80),
            new("Others", "أخرى", "Package", "#64748b", 90)
        };

        private readonly IAssetRepository _repo;
        private readonly IAssetFileStorageService _storage;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchContext _branchContext;
        private readonly ILogger<AssetService> _logger;

        public AssetService(
            IAssetRepository repo,
            IAssetFileStorageService storage,
            ITenantResolver tenantResolver,
            IBranchContext branchContext,
            ILogger<AssetService> logger)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PaginatedResponse<AssetListItemDto>> GetPagedAsync(AssetQueryDto query, bool isArabic, AssetActor actor, CancellationToken ct)
            => await _repo.GetPagedAsync(query, await GetCurrentBranchIdAsync(ct), isArabic, AssignedUserFilter(actor), ct);

        public async Task<List<AssetListItemDto>> GetExportRowsAsync(AssetQueryDto query, bool isArabic, AssetActor actor, CancellationToken ct)
            => await _repo.GetExportRowsAsync(query, await GetCurrentBranchIdAsync(ct), isArabic, AssignedUserFilter(actor), ExportMaxRows, ct);

        public async Task<AssetDashboardDto> GetDashboardAsync(bool isArabic, AssetActor actor, CancellationToken ct)
            => await _repo.GetDashboardAsync(await GetCurrentBranchIdAsync(ct), isArabic, AssignedUserFilter(actor), ct);

        public async Task<AssetDetailsDto> GetDetailsAsync(Guid id, bool isArabic, AssetActor actor, CancellationToken ct)
        {
            await AssertCanViewAsync(id, actor, ct);
            return await _repo.GetDetailsAsync(id, await GetCurrentBranchIdAsync(ct), isArabic, ControllerRoute, _storage.GetPublicUrl, ct)
                ?? throw new NotFoundException(nameof(Asset), id);
        }

        public async Task<AssetDetailsDto> CreateAsync(AssetCreateDto dto, bool isArabic, AssetActor actor, CancellationToken ct)
        {
            EnsureCanManage(actor);
            await ValidateReferencesAsync(dto.AssetCategoryId, dto.AssignedToEmployeeId, ct);
            ValidateWarranty(dto.WarrantyStartDate, dto.WarrantyEndDate);

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                BranchId = await GetCurrentBranchIdAsync(ct)
            };
            ApplyDto(asset, dto, actor);
            asset.AssetCode = await GenerateAssetCodeAsync(ct);
            asset.QRCode = $"/ntxadminpos/assets/{asset.Id}";
            asset.CreatedById = actor.UserId;
            asset.CreatedBy = actor.Name;

            await _repo.AddAssetAsync(asset, ct);
            await AddLogAsync(asset, AssetActivityActionType.Created, null, Snapshot(asset), actor, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation("Asset {AssetId} created with code {AssetCode} by {UserId}", asset.Id, asset.AssetCode, actor.UserId);
            return await GetDetailsAsync(asset.Id, isArabic, actor, ct);
        }

        public async Task<AssetDetailsDto> UpdateAsync(Guid id, AssetUpdateDto dto, bool isArabic, AssetActor actor, CancellationToken ct)
        {
            EnsureCanManage(actor);
            var asset = await _repo.GetTrackedAssetAsync(id, await GetCurrentBranchIdAsync(ct), ct) ?? throw new NotFoundException(nameof(Asset), id);
            await ValidateReferencesAsync(dto.AssetCategoryId, dto.AssignedToEmployeeId, ct);
            ValidateWarranty(dto.WarrantyStartDate, dto.WarrantyEndDate);

            var oldSnapshot = Snapshot(asset);
            var oldStatus = asset.Status;
            var oldAssigned = asset.AssignedToEmployeeId;
            var nextStatus = ParseEnum(dto.Status, AssetStatus.Active);
            ValidateStatusTransition(oldStatus, nextStatus);

            ApplyDto(asset, dto, actor);
            await AddUpdateLogsAsync(asset, oldSnapshot, oldStatus, oldAssigned, actor, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation("Asset {AssetId} updated by {UserId}", asset.Id, actor.UserId);
            return await GetDetailsAsync(asset.Id, isArabic, actor, ct);
        }

        public async Task SoftDeleteAsync(Guid id, AssetActor actor, CancellationToken ct)
        {
            EnsureCanManage(actor);
            var asset = await _repo.GetTrackedAssetAsync(id, await GetCurrentBranchIdAsync(ct), ct) ?? throw new NotFoundException(nameof(Asset), id);
            asset.DeletedAt = DateTime.UtcNow;
            asset.UpdatedById = actor.UserId;
            asset.UpdatedBy = actor.Name;
            await AddLogAsync(asset, AssetActivityActionType.Deleted, Snapshot(asset), null, actor, ct);
            await _repo.SaveChangesAsync(ct);
        }

        public async Task RestoreAsync(Guid id, AssetActor actor, CancellationToken ct)
        {
            EnsureCanManage(actor);
            var asset = await _repo.GetTrackedAssetIgnoringFiltersAsync(id, await GetCurrentBranchIdAsync(ct), ct) ?? throw new NotFoundException(nameof(Asset), id);
            asset.DeletedAt = null;
            asset.IsActive = true;
            asset.UpdatedById = actor.UserId;
            asset.UpdatedBy = actor.Name;
            await AddLogAsync(asset, AssetActivityActionType.Restored, null, Snapshot(asset), actor, ct);
            await _repo.SaveChangesAsync(ct);
        }

        public async Task<List<AssetCategoryDto>> GetCategoriesAsync(bool activeOnly, bool isArabic, CancellationToken ct)
        {
            await EnsureDefaultCategoriesAsync(ct);
            return await _repo.GetCategoriesAsync(activeOnly, isArabic, ct);
        }

        public async Task<AssetCategoryDto> CreateCategoryAsync(AssetCategoryCreateDto dto, bool isArabic, AssetActor actor, CancellationToken ct)
        {
            EnsureCanManage(actor);
            await ValidateCategoryNameAsync(dto.NameEn, dto.NameAr, null, ct);
            var category = BuildCategory(dto);
            await _repo.AddCategoryAsync(category, ct);
            await _repo.SaveChangesAsync(ct);
            return (await _repo.GetCategoriesAsync(false, isArabic, ct)).First(c => c.Id == category.Id);
        }

        public async Task<AssetCategoryDto> UpdateCategoryAsync(Guid id, AssetCategoryUpdateDto dto, bool isArabic, AssetActor actor, CancellationToken ct)
        {
            EnsureCanManage(actor);
            var category = await _repo.GetTrackedCategoryAsync(id, ct) ?? throw new NotFoundException(nameof(AssetCategory), id);
            await ValidateCategoryNameAsync(dto.NameEn, dto.NameAr, id, ct);
            ApplyCategoryDto(category, dto);
            await _repo.SaveChangesAsync(ct);
            return (await _repo.GetCategoriesAsync(false, isArabic, ct)).First(c => c.Id == id);
        }

        public async Task DeleteCategoryAsync(Guid id, AssetActor actor, CancellationToken ct)
        {
            EnsureCanManage(actor);
            var category = await _repo.GetTrackedCategoryAsync(id, ct) ?? throw new NotFoundException(nameof(AssetCategory), id);
            if (await _repo.CategoryInUseAsync(id, ct))
                throw new ValidationException("Category is assigned to assets and cannot be deleted.");

            category.DeletedAt = DateTime.UtcNow;
            await _repo.SaveChangesAsync(ct);
        }

        public async Task<AssetAttachmentDto> AddAttachmentAsync(
            Guid assetId,
            IFormFile file,
            string? attachmentType,
            Guid? maintenanceRecordId,
            AssetActor actor,
            CancellationToken ct)
        {
            EnsureCanManage(actor);
            var asset = await _repo.GetTrackedAssetAsync(assetId, await GetCurrentBranchIdAsync(ct), ct) ?? throw new NotFoundException(nameof(Asset), assetId);
            ValidateMaintenanceLink(asset, maintenanceRecordId);
            var type = ParseEnum(attachmentType, AssetAttachmentType.Other);
            var stored = await _storage.SaveAsync(file, type, ct);

            try
            {
                var attachment = BuildAttachment(asset, stored, type, maintenanceRecordId, actor);
                await _repo.AddAttachmentAsync(attachment, ct);
                await AddLogAsync(asset, AssetActivityActionType.AttachmentUploaded, null, attachment.OriginalFileName, actor, ct);
                await _repo.SaveChangesAsync(ct);
                return MapAttachment(attachment);
            }
            catch
            {
                await _storage.DeleteAsync(stored.RelativePath, CancellationToken.None);
                throw;
            }
        }

        public async Task DeleteAttachmentAsync(Guid assetId, Guid attachmentId, AssetActor actor, CancellationToken ct)
        {
            EnsureCanManage(actor);
            var attachment = await _repo.GetAttachmentAsync(assetId, attachmentId, ct)
                ?? throw new NotFoundException(nameof(AssetAttachment), attachmentId);
            var path = attachment.FilePath;
            var asset = await _repo.GetTrackedAssetAsync(assetId, await GetCurrentBranchIdAsync(ct), ct) ?? throw new NotFoundException(nameof(Asset), assetId);

            await _repo.RemoveAttachmentAsync(attachment, ct);
            await AddLogAsync(asset, AssetActivityActionType.AttachmentDeleted, attachment.OriginalFileName, null, actor, ct);
            await _repo.SaveChangesAsync(ct);
            await _storage.DeleteAsync(path, CancellationToken.None);
        }

        public async Task<(Stream Stream, string FileName, string MimeType)> OpenAttachmentForDownloadAsync(
            Guid assetId,
            Guid attachmentId,
            AssetActor actor,
            CancellationToken ct)
        {
            await AssertCanViewAsync(assetId, actor, ct);
            var attachment = await _repo.GetAttachmentAsync(assetId, attachmentId, ct)
                ?? throw new NotFoundException(nameof(AssetAttachment), attachmentId);

            try
            {
                return (await _storage.OpenReadAsync(attachment.FilePath, ct), attachment.OriginalFileName, attachment.MimeType);
            }
            catch (FileNotFoundException)
            {
                throw new NotFoundException("Attachment file is missing.");
            }
        }

        public async Task<PaginatedResponse<AssetMaintenanceRecordDto>> GetMaintenanceAsync(
            AssetMaintenanceQueryDto query,
            bool isArabic,
            AssetActor actor,
            CancellationToken ct)
            => await _repo.GetMaintenancePagedAsync(query, await GetCurrentBranchIdAsync(ct), isArabic, AssignedUserFilter(actor), ct);

        public async Task<AssetMaintenanceRecordDto> AddMaintenanceAsync(Guid assetId, AssetMaintenanceCreateDto dto, bool isArabic, AssetActor actor, CancellationToken ct)
        {
            EnsureCanManage(actor);
            var asset = await _repo.GetTrackedAssetAsync(assetId, await GetCurrentBranchIdAsync(ct), ct) ?? throw new NotFoundException(nameof(Asset), assetId);
            var oldStatus = asset.Status;
            var maintenance = BuildMaintenance(asset, dto, actor);
            ApplyMaintenanceLifecycle(asset, maintenance.Status, actor);

            await _repo.AddMaintenanceAsync(maintenance, ct);
            await AddLogAsync(asset, AssetActivityActionType.MaintenanceAdded, null, SnapshotMaintenance(maintenance), actor, ct);
            if (oldStatus != asset.Status)
                await AddLogAsync(asset, AssetActivityActionType.StatusChanged, oldStatus.ToString(), asset.Status.ToString(), actor, ct);
            await _repo.SaveChangesAsync(ct);

            return (await _repo.GetMaintenanceForAssetAsync(assetId, isArabic, ct)).First(m => m.Id == maintenance.Id);
        }

        public async Task<PaginatedResponse<AssetActivityLogDto>> GetLogsAsync(
            AssetActivityLogQueryDto query,
            bool isArabic,
            AssetActor actor,
            CancellationToken ct)
            => await _repo.GetLogsPagedAsync(query, await GetCurrentBranchIdAsync(ct), isArabic, AssignedUserFilter(actor), ct);

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
            => (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

        private async Task AssertCanViewAsync(Guid assetId, AssetActor actor, CancellationToken ct)
        {
            if (actor.CanManageAssets) return;
            if (!actor.UserId.HasValue || !await _repo.CanUserAccessAssetAsync(assetId, actor.UserId.Value, ct))
                throw new ForbiddenException("You can only view assets assigned to you.");
        }

        private static void EnsureCanManage(AssetActor actor)
        {
            if (!actor.CanManageAssets)
                throw new ForbiddenException("You do not have permission to manage assets.");
        }

        private async Task ValidateReferencesAsync(Guid categoryId, Guid? assignedEmployeeId, CancellationToken ct)
        {
            if (!await _repo.CategoryExistsAsync(categoryId, ct))
                throw new ValidationException("Asset category is required.");
            if (assignedEmployeeId.HasValue && !await _repo.StaffProfileExistsAsync(assignedEmployeeId.Value, ct))
                throw new ValidationException("Assigned employee was not found.");
        }

        private async Task<string> GenerateAssetCodeAsync(CancellationToken ct)
        {
            var today = DateTime.UtcNow.Date;
            var count = await _repo.CountCreatedOnDateAsync(today, ct);
            for (var i = 1; i <= 100; i++)
            {
                var code = $"{CodePrefix}-{today:yyyyMMdd}-{count + i:D5}";
                if (!await _repo.AssetCodeExistsAsync(code, null, ct))
                    return code;
            }

            return $"{CodePrefix}-{today:yyyyMMdd}-{Guid.NewGuid():N}"[..40];
        }

        private static void ApplyDto(Asset asset, AssetCreateDto dto, AssetActor actor)
        {
            asset.AssetNameEn = RequiredTrim(dto.AssetNameEn, "Asset English name is required.");
            asset.AssetNameAr = RequiredTrim(dto.AssetNameAr, "Asset Arabic name is required.");
            asset.DescriptionEn = TrimToNull(dto.DescriptionEn);
            asset.DescriptionAr = TrimToNull(dto.DescriptionAr);
            asset.AssetCategoryId = dto.AssetCategoryId;
            asset.AssetType = TrimToNull(dto.AssetType);
            asset.Brand = TrimToNull(dto.Brand);
            asset.Model = TrimToNull(dto.Model);
            asset.SerialNumber = TrimToNull(dto.SerialNumber);
            asset.Barcode = TrimToNull(dto.Barcode);
            asset.PurchaseDate = EnsureUtcOrNull(dto.PurchaseDate);
            asset.PurchaseCost = dto.PurchaseCost;
            asset.CurrentValue = dto.CurrentValue;
            asset.WarrantyStartDate = EnsureUtcOrNull(dto.WarrantyStartDate);
            asset.WarrantyEndDate = EnsureUtcOrNull(dto.WarrantyEndDate);
            asset.SupplierName = TrimToNull(dto.SupplierName);
            asset.Status = ParseEnum(dto.Status, AssetStatus.Active);
            asset.Condition = ParseEnum(dto.Condition, AssetCondition.Good);
            asset.AssignedToEmployeeId = dto.AssignedToEmployeeId;
            asset.AssignedLocation = TrimToNull(dto.AssignedLocation);
            asset.Department = TrimToNull(dto.Department);
            asset.Notes = TrimToNull(dto.Notes);
            asset.IsActive = dto.IsActive;
            asset.UpdatedById = actor.UserId;
            asset.UpdatedBy = actor.Name;
        }

        private async Task AddUpdateLogsAsync(Asset asset, string oldSnapshot, AssetStatus oldStatus, Guid? oldAssigned, AssetActor actor, CancellationToken ct)
        {
            if (oldStatus != asset.Status)
                await AddLogAsync(asset, AssetActivityActionType.StatusChanged, oldStatus.ToString(), asset.Status.ToString(), actor, ct);
            if (oldAssigned != asset.AssignedToEmployeeId)
                await AddLogAsync(asset, AssetActivityActionType.Assigned, oldAssigned?.ToString(), asset.AssignedToEmployeeId?.ToString(), actor, ct);
            await AddLogAsync(asset, AssetActivityActionType.Updated, oldSnapshot, Snapshot(asset), actor, ct);
        }

        private static void ValidateStatusTransition(AssetStatus oldStatus, AssetStatus nextStatus)
        {
            if (oldStatus == nextStatus) return;
            if (!AllowedStatusTransitions.TryGetValue(oldStatus, out var allowed) || !allowed.Contains(nextStatus))
                throw new ValidationException($"Asset status cannot change from {oldStatus} to {nextStatus}.");
        }

        private static void ValidateWarranty(DateTime? start, DateTime? end)
        {
            if (start.HasValue && end.HasValue && EnsureUtc(end.Value) < EnsureUtc(start.Value))
                throw new ValidationException("Warranty end date cannot be before warranty start date.");
        }

        private async Task EnsureDefaultCategoriesAsync(CancellationToken ct)
        {
            if (await _repo.HasAnyCategoryAsync(ct)) return;
            foreach (var item in DefaultCategories)
            {
                await _repo.AddCategoryAsync(new AssetCategory
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantResolver.GetTenantId(),
                    NameEn = item.NameEn,
                    NameAr = item.NameAr,
                    Icon = item.Icon,
                    Color = item.Color,
                    SortOrder = item.SortOrder,
                    IsActive = true
                }, ct);
            }
            await _repo.SaveChangesAsync(ct);
        }

        private async Task ValidateCategoryNameAsync(string nameEn, string nameAr, Guid? excludeId, CancellationToken ct)
        {
            var en = RequiredTrim(nameEn, "English category name is required.");
            var ar = RequiredTrim(nameAr, "Arabic category name is required.");
            if (await _repo.CategoryNameExistsAsync(en, ar, excludeId, ct))
                throw new ValidationException("Asset category name already exists.");
        }

        private AssetCategory BuildCategory(AssetCategoryCreateDto dto)
        {
            var category = new AssetCategory { Id = Guid.NewGuid(), TenantId = _tenantResolver.GetTenantId() };
            ApplyCategoryDto(category, dto);
            return category;
        }

        private static void ApplyCategoryDto(AssetCategory category, AssetCategoryCreateDto dto)
        {
            category.NameEn = RequiredTrim(dto.NameEn, "English category name is required.");
            category.NameAr = RequiredTrim(dto.NameAr, "Arabic category name is required.");
            category.Icon = TrimToNull(dto.Icon);
            category.Color = TrimToNull(dto.Color);
            category.IsActive = dto.IsActive;
            category.SortOrder = dto.SortOrder;
        }

        private static AssetAttachment BuildAttachment(
            Asset asset,
            AssetStoredFileResult stored,
            AssetAttachmentType type,
            Guid? maintenanceRecordId,
            AssetActor actor)
            => new()
            {
                Id = Guid.NewGuid(),
                TenantId = asset.TenantId,
                AssetId = asset.Id,
                MaintenanceRecordId = maintenanceRecordId,
                FileName = stored.StoredFileName,
                OriginalFileName = stored.OriginalFileName,
                FileExtension = stored.FileExtension,
                FileSize = stored.FileSize,
                MimeType = stored.MimeType,
                FilePath = stored.RelativePath,
                FileUrl = stored.PublicUrl,
                AttachmentType = type,
                UploadedAt = DateTime.UtcNow,
                UploadedById = actor.UserId,
                UploadedBy = actor.Name
            };

        private AssetAttachmentDto MapAttachment(AssetAttachment attachment)
            => new()
            {
                Id = attachment.Id,
                AssetId = attachment.AssetId,
                MaintenanceRecordId = attachment.MaintenanceRecordId,
                FileName = attachment.FileName,
                OriginalFileName = attachment.OriginalFileName,
                FileExtension = attachment.FileExtension,
                FileSize = attachment.FileSize,
                MimeType = attachment.MimeType,
                FilePath = attachment.FilePath,
                FileUrl = _storage.GetPublicUrl(attachment.FilePath) ?? string.Empty,
                DownloadUrl = $"{ControllerRoute}/{attachment.AssetId}/attachments/{attachment.Id}/download",
                AttachmentType = attachment.AttachmentType.ToString(),
                UploadedAt = attachment.UploadedAt,
                UploadedById = attachment.UploadedById,
                UploadedBy = attachment.UploadedBy
            };

        private static AssetMaintenanceRecord BuildMaintenance(Asset asset, AssetMaintenanceCreateDto dto, AssetActor actor)
            => new()
            {
                Id = Guid.NewGuid(),
                TenantId = asset.TenantId,
                AssetId = asset.Id,
                MaintenanceType = RequiredTrim(dto.MaintenanceType, "Maintenance type is required."),
                Description = RequiredTrim(dto.Description, "Maintenance description is required."),
                Cost = dto.Cost,
                PerformedBy = TrimToNull(dto.PerformedBy),
                Vendor = TrimToNull(dto.Vendor),
                MaintenanceDate = EnsureUtc(dto.MaintenanceDate),
                NextMaintenanceDate = EnsureUtcOrNull(dto.NextMaintenanceDate),
                Status = ParseEnum(dto.Status, AssetMaintenanceStatus.Completed),
                Notes = TrimToNull(dto.Notes),
                CreatedById = actor.UserId,
                CreatedBy = actor.Name
            };

        private static void ApplyMaintenanceLifecycle(Asset asset, AssetMaintenanceStatus status, AssetActor actor)
        {
            if (status is AssetMaintenanceStatus.Scheduled or AssetMaintenanceStatus.InProgress or AssetMaintenanceStatus.Overdue)
            {
                ValidateStatusTransition(asset.Status, AssetStatus.InMaintenance);
                asset.Status = AssetStatus.InMaintenance;
                asset.UpdatedById = actor.UserId;
                asset.UpdatedBy = actor.Name;
            }
        }

        private static void ValidateMaintenanceLink(Asset asset, Guid? maintenanceRecordId)
        {
            if (maintenanceRecordId.HasValue && asset.MaintenanceRecords.All(m => m.Id != maintenanceRecordId.Value))
                throw new ValidationException("Maintenance record does not belong to this asset.");
        }

        private async Task AddLogAsync(Asset asset, AssetActivityActionType action, object? oldValue, object? newValue, AssetActor actor, CancellationToken ct)
        {
            await _repo.AddLogAsync(new AssetActivityLog
            {
                Id = Guid.NewGuid(),
                TenantId = asset.TenantId,
                AssetId = asset.Id,
                ActionType = action,
                OldValue = Serialise(oldValue),
                NewValue = Serialise(newValue),
                PerformedById = actor.UserId,
                PerformedBy = actor.Name,
                Timestamp = DateTime.UtcNow,
                IpAddress = actor.IpAddress,
                DeviceInfo = actor.DeviceInfo
            }, ct);
        }

        private static string Snapshot(Asset asset)
            => JsonSerializer.Serialize(new
            {
                asset.AssetCode,
                asset.AssetNameEn,
                asset.AssetNameAr,
                asset.AssetCategoryId,
                asset.AssetType,
                asset.Brand,
                asset.Model,
                asset.SerialNumber,
                asset.Barcode,
                asset.PurchaseDate,
                asset.PurchaseCost,
                asset.CurrentValue,
                asset.WarrantyEndDate,
                Status = asset.Status.ToString(),
                Condition = asset.Condition.ToString(),
                asset.AssignedToEmployeeId,
                asset.AssignedLocation,
                asset.Department,
                asset.IsActive
            }, JsonOptions);

        private static string SnapshotMaintenance(AssetMaintenanceRecord record)
            => JsonSerializer.Serialize(new
            {
                record.MaintenanceType,
                record.Cost,
                record.PerformedBy,
                record.Vendor,
                record.MaintenanceDate,
                record.NextMaintenanceDate,
                Status = record.Status.ToString()
            }, JsonOptions);

        private static string? Serialise(object? value)
        {
            if (value is null) return null;
            return value is string text ? text : JsonSerializer.Serialize(value, JsonOptions);
        }

        private static Guid? AssignedUserFilter(AssetActor actor)
            => actor.CanManageAssets ? null : actor.UserId ?? Guid.Empty;

        private static TEnum ParseEnum<TEnum>(string? raw, TEnum fallback) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            var normalised = raw.Replace(" ", string.Empty).Replace("-", string.Empty);
            return Enum.TryParse<TEnum>(normalised, true, out var parsed) ? parsed : fallback;
        }

        private static string RequiredTrim(string? value, string message)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? throw new ValidationException(message) : trimmed;
        }

        private static string? TrimToNull(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static DateTime? EnsureUtcOrNull(DateTime? value)
            => value.HasValue ? EnsureUtc(value.Value) : null;

        private static DateTime EnsureUtc(DateTime value)
            => value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

        public string BuildCsv(IEnumerable<AssetListItemDto> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AssetCode,AssetName,Category,Status,Condition,AssignedTo,Location,Department,PurchaseCost,CurrentValue,WarrantyEndDate");
            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(row.AssetCode),
                    Csv(row.AssetName),
                    Csv(row.CategoryName),
                    Csv(row.Status),
                    Csv(row.Condition),
                    Csv(row.AssignedToEmployeeName),
                    Csv(row.AssignedLocation),
                    Csv(row.Department),
                    Csv(row.PurchaseCost?.ToString("0.00")),
                    Csv(row.CurrentValue?.ToString("0.00")),
                    Csv(row.WarrantyEndDate?.ToString("yyyy-MM-dd"))
                }));
            }
            return sb.ToString();
        }

        private static string Csv(string? value)
        {
            var text = value ?? string.Empty;
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        private sealed record AssetDefaultCategory(string NameEn, string NameAr, string Icon, string Color, int SortOrder);
    }
}
