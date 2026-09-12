using System.Text;
using System.Text.Json;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    public class WasteLogService : IWasteLogService
    {
        private const string ProductUnit = "Item";
        private const string WasteEditReversalNote = "Reversed before waste log edit.";
        private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IWasteLogRepository _repository;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchContext _branchContext;
        private readonly IInventoryService _inventoryService;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<WasteLogService> _logger;

        public WasteLogService(
            IWasteLogRepository repository,
            ITenantResolver tenantResolver,
            IBranchContext branchContext,
            IInventoryService inventoryService,
            ICurrentUserAccessor currentUser,
            ILogger<WasteLogService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WasteLogPageDto> GetPagedAsync(WasteLogQueryDto query, CancellationToken ct = default)
        {
            query.BranchId = await ResolveReadBranchIdAsync(query.BranchId, ct);
            return await _repository.GetPagedAsync(query, ct);
        }

        public Task<IReadOnlyList<WasteEmployeeOptionDto>> SearchEmployeesAsync(string? search, int limit, CancellationToken ct = default)
            => _repository.SearchEmployeesAsync(search, limit, ct);

        public async Task<WasteLogDto> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var log = await _repository.GetByIdAsync(id, includeAudit: true, ct)
                ?? throw new NotFoundException("Waste log", id);

            await EnsureCurrentBranchAsync(log, ct);
            return MapToDto(log, includeAudit: true);
        }

        public async Task<WasteLogSummaryDto> GetSummaryAsync(WasteLogQueryDto query, CancellationToken ct = default)
        {
            query.BranchId = await ResolveReadBranchIdAsync(query.BranchId, ct);
            var effectiveQuery = EnsureSummaryDateRange(query);
            var result = await _repository.GetSummaryAsync(effectiveQuery, ct);

            _logger.LogDebug(
                "Waste summary included {TotalCount} records. Manual={ManualCount}, Expiry={ExpiryCount}, Cancel={CancelCount}, Cost={TotalWasteCost}, SaleLoss={TotalSalePriceLoss}",
                result.TotalCount,
                result.ManualCount,
                result.ExpiryCount,
                result.CancelCount,
                result.TotalWasteCost,
                result.TotalSalePriceLoss);

            return result;
        }

        public async Task<WasteLogAnalyticsDto> GetAnalyticsAsync(WasteLogQueryDto query, CancellationToken ct = default)
        {
            query.BranchId = await ResolveReadBranchIdAsync(query.BranchId, ct);
            var result = await _repository.GetAnalyticsAsync(query, ct);

            _logger.LogDebug(
                "Waste analytics included {TotalCount} records. Approved={ApprovedCount}, Pending={PendingCount}, Cost={TotalWasteCost}, Quantity={TotalQuantity}",
                result.TotalCount,
                result.ApprovedCount,
                result.PendingApprovalCount,
                result.TotalWasteCost,
                result.TotalQuantity);

            return result;
        }

        public async Task<byte[]> ExportCsvAsync(WasteLogQueryDto query, CancellationToken ct = default)
        {
            query.BranchId = await ResolveReadBranchIdAsync(query.BranchId, ct);
            query.Page = 1;
            query.Limit = 10000;
            var rows = await _repository.GetPagedAsync(query, ct);
            var sb = new StringBuilder();

            sb.AppendLine("#,Waste # | رقم الهدر,Type | النوع,Category | الفئة,Item Name | اسم الصنف,Quantity | الكمية,Unit | الوحدة,Status | الحالة,Cost | التكلفة,Sales Loss | خسارة البيع,Reason | السبب,Logged By | بواسطة,Order # | رقم الطلب,Batch # | رقم الدفعة,Date & Time | التاريخ والوقت,Employee | الموظف");

            var i = 1;
            foreach (var row in rows.Data)
            {
                sb.AppendLine(string.Join(",",
                    i++,
                    CsvEscape(row.WasteNumber),
                    CsvEscape(row.WasteType),
                    CsvEscape(row.WasteCategory),
                    CsvEscape(row.ItemName),
                    row.Quantity.ToString("F3"),
                    CsvEscape(row.Unit),
                    CsvEscape(row.Status),
                    row.CostAmount.ToString("F2"),
                    row.SalePriceLoss.ToString("F2"),
                    CsvEscape(row.Reason),
                    CsvEscape(row.LoggedBy),
                    CsvEscape(row.OrderNumber),
                    CsvEscape(row.BatchNumber),
                    row.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    CsvEscape(string.Join("; ", row.Employees.Select(e => e.Name)))));
            }

            return Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();
        }

        public async Task<WasteLogDto> CreateAsync(
            WasteLogCreateDto request,
            Guid? userId,
            string? userName,
            CancellationToken ct = default)
        {
            var wasteType = WasteLogValidator.ValidateCreate(request);
            var tenantId = _tenantResolver.GetTenantId();
            var branchId = await GetCurrentBranchIdAsync(ct);
            var createdBy = NormalizeName(userName);

            await ValidateOrderAsync(request.OrderId, branchId, ct);
            var log = request.ProductId.HasValue
                ? await BuildProductWasteAsync(request, wasteType, tenantId, branchId, userId, createdBy, ct)
                : await BuildMaterialWasteAsync(request, wasteType, tenantId, branchId, userId, createdBy, ct);

            await ApplyStaffMealEmployeesAsync(log, request, wasteType, ct);

            await using var transaction = await _repository.BeginTransactionAsync(ct);
            await _repository.AddAsync(log, ct);
            var costAmount = log.IsAffectingInventory
                ? await DeductInventoryForApprovedWasteAsync(log, userId, createdBy, request.Notes, ct)
                : 0m;

            log.Status = WasteLogStatus.Approved;
            log.ApprovedById = userId;
            log.ApprovedBy = createdBy;
            log.DecisionAt = DateTime.UtcNow;
            log.CostAmount = costAmount > 0 ? costAmount : log.CostAmount;
            log.Amount = log.SalePriceLoss > 0 ? log.SalePriceLoss : log.CostAmount;

            await _repository.AddAuditAsync(BuildAudit(log, WasteLogAuditAction.Created, null, log.Status, userId, createdBy, request.Notes), ct);
            await _repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Manual waste log {WasteLogId} created as {WasteNumber} by {UserId}; inventory cost={CostAmount}",
                log.Id,
                log.WasteNumber,
                userId,
                log.CostAmount);

            return MapToDto(log, includeAudit: false);
        }

        public async Task<WasteLogDto> UpdateAsync(
            Guid id,
            WasteLogUpdateDto request,
            Guid? userId,
            string? userName,
            CancellationToken ct = default)
        {
            var log = await _repository.GetByIdAsync(id, includeAudit: false, ct)
                ?? throw new NotFoundException("Waste log", id);

            await EnsureCurrentBranchAsync(log, ct);
            WasteLogValidator.EnsureEditable(log);

            var updatedBy = NormalizeName(userName);
            var previousValues = BuildAuditSnapshot(log);

            await using var transaction = await _repository.BeginTransactionAsync(ct);
            if (IsManualWaste(log))
                await UpdateManualWasteAsync(log, request, userId, updatedBy, ct);
            else
                await UpdateSourceLinkedWasteAsync(log, request, ct);

            log.Amount = log.SalePriceLoss > 0 ? log.SalePriceLoss : log.CostAmount;

            var newValues = BuildAuditSnapshot(log);
            await _repository.AddAuditAsync(BuildUpdateAudit(log, previousValues, newValues, userId, updatedBy), ct);
            await _repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Waste log {WasteLogId} updated by {UserId}; category={Category}; inventory affected={AffectsInventory}; cost={CostAmount}",
                log.Id,
                userId,
                log.Category,
                log.IsAffectingInventory,
                log.CostAmount);

            return await GetByIdAsync(id, ct);
        }

        public async Task<WasteLogDto> ApproveAsync(
            Guid id,
            WasteLogDecisionDto request,
            Guid? userId,
            string? userName,
            CancellationToken ct = default)
        {
            var log = await _repository.GetByIdAsync(id, includeAudit: false, ct)
                ?? throw new NotFoundException("Waste log", id);

            await EnsureCurrentBranchAsync(log, ct);
            WasteLogValidator.EnsurePending(log);
            var previousStatus = log.Status;
            var approvedBy = NormalizeName(userName);

            await using var transaction = await _repository.BeginTransactionAsync(ct);
            var costAmount = log.IsAffectingInventory
                ? await DeductInventoryForApprovedWasteAsync(log, userId, approvedBy, request.Notes, ct)
                : 0m;

            log.Status = WasteLogStatus.Approved;
            log.ApprovedById = userId;
            log.ApprovedBy = approvedBy;
            log.DecisionAt = DateTime.UtcNow;
            log.CostAmount = costAmount > 0 ? costAmount : log.CostAmount;
            log.Amount = log.SalePriceLoss > 0 ? log.SalePriceLoss : log.CostAmount;

            await _repository.AddAuditAsync(BuildAudit(log, WasteLogAuditAction.Approved, previousStatus, log.Status, userId, approvedBy, request.Notes), ct);
            await _repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Manual waste log {WasteLogId} approved by {UserId}; inventory affected={AffectsInventory}; cost={CostAmount}",
                log.Id,
                userId,
                log.IsAffectingInventory,
                log.CostAmount);

            return await GetByIdAsync(id, ct);
        }

        public async Task<WasteLogDto> RejectAsync(
            Guid id,
            WasteLogDecisionDto request,
            Guid? userId,
            string? userName,
            CancellationToken ct = default)
        {
            var log = await _repository.GetByIdAsync(id, includeAudit: false, ct)
                ?? throw new NotFoundException("Waste log", id);

            await EnsureCurrentBranchAsync(log, ct);
            WasteLogValidator.EnsurePending(log);
            var previousStatus = log.Status;
            var rejectedBy = NormalizeName(userName);

            await using var transaction = await _repository.BeginTransactionAsync(ct);
            log.Status = WasteLogStatus.Rejected;
            log.ApprovedById = userId;
            log.ApprovedBy = rejectedBy;
            log.DecisionAt = DateTime.UtcNow;
            log.IsAffectingInventory = false;

            await _repository.AddAuditAsync(BuildAudit(log, WasteLogAuditAction.Rejected, previousStatus, log.Status, userId, rejectedBy, request.Notes), ct);
            await _repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Manual waste log {WasteLogId} rejected by {UserId}", log.Id, userId);
            return await GetByIdAsync(id, ct);
        }

        private async Task<WasteLog> BuildProductWasteAsync(
            WasteLogCreateDto request,
            ManualWasteType wasteType,
            Guid tenantId,
            Guid branchId,
            Guid? userId,
            string? createdBy,
            CancellationToken ct)
        {
            var productId = request.ProductId!.Value;
            var product = await _repository.GetProductAsync(productId, ct)
                ?? throw new NotFoundException("Product", productId);

            var saleLoss = request.SalePriceLoss ?? ResolveSalePrice(product) * request.Quantity;
            var estimatedCost = (product.CostPrice ?? 0m) * request.Quantity;

            var log = BuildBaseLog(request, tenantId, branchId, userId, createdBy);
            log.Category = WasteCategory.ManualProduct;
            log.WasteType = wasteType;
            log.ItemId = product.Id;
            log.ProductId = product.Id;
            log.ItemName = product.Name;
            log.ItemNameAr = product.NameAr;
            log.Unit = NormalizeUnit(request.Unit, ProductUnit);
            log.CostAmount = estimatedCost;
            log.SalePriceLoss = saleLoss;
            log.Amount = saleLoss > 0 ? saleLoss : estimatedCost;
            return log;
        }

        private async Task<WasteLog> BuildMaterialWasteAsync(
            WasteLogCreateDto request,
            ManualWasteType wasteType,
            Guid tenantId,
            Guid branchId,
            Guid? userId,
            string? createdBy,
            CancellationToken ct)
        {
            var materialId = request.MaterialId!.Value;
            var material = await _repository.GetMaterialAsync(materialId, ct)
                ?? throw new NotFoundException("Raw material", materialId);

            var quantity = InventoryUnitConverter.Convert(request.Quantity, request.Unit, material.Unit);
            var estimatedCost = material.CostPerUnit * quantity;
            var saleLoss = request.SalePriceLoss ?? 0m;

            var log = BuildBaseLog(request, tenantId, branchId, userId, createdBy);
            log.Category = WasteCategory.ManualMaterial;
            log.WasteType = wasteType;
            log.ItemId = material.Id;
            log.MaterialId = material.Id;
            log.ItemName = material.Name;
            log.ItemNameAr = material.NameAr;
            log.Quantity = quantity;
            log.Unit = material.Unit.ToString();
            log.CostAmount = estimatedCost;
            log.SalePriceLoss = saleLoss;
            log.Amount = saleLoss > 0 ? saleLoss : estimatedCost;
            return log;
        }

        private WasteLog BuildBaseLog(WasteLogCreateDto request, Guid tenantId, Guid branchId, Guid? userId, string? createdBy)
        {
            return new WasteLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Type = WasteLogType.Waste,
                WasteNumber = GenerateWasteNumber(),
                Quantity = request.Quantity,
                Reason = request.Reason.Trim(),
                Notes = NormalizeText(request.Notes),
                Status = WasteLogStatus.Approved,
                WasteDate = NormalizeWasteDate(request.WasteDate) ?? DateTime.UtcNow,
                LoggedById = userId,
                LoggedByName = createdBy,
                CreatedBy = createdBy,
                BranchId = branchId,
                OrderId = request.OrderId,
                SourceOrderId = request.OrderId,
                AttachmentUrl = NormalizeText(request.AttachmentUrl),
                IsAffectingInventory = request.IsAffectingInventory,
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task ApplyUpdatedWasteFieldsAsync(
            WasteLog log,
            WasteLogUpdateDto request,
            ManualWasteType wasteType,
            CancellationToken ct)
        {
            ApplyBaseEditableFields(log, request, wasteType);
            if (request.ProductId.HasValue)
            {
                await ApplyProductWasteFieldsAsync(log, request, ct);
                return;
            }

            await ApplyMaterialWasteFieldsAsync(log, request, ct);
        }

        private async Task UpdateManualWasteAsync(
            WasteLog log,
            WasteLogUpdateDto request,
            Guid? userId,
            string? updatedBy,
            CancellationToken ct)
        {
            var wasteType = WasteLogValidator.ValidateUpdate(request);
            await ValidateOrderAsync(request.OrderId, log.BranchId, ct);
            await ReverseCurrentInventoryImpactAsync(log, userId, updatedBy, ct);
            await ApplyUpdatedWasteFieldsAsync(log, request, wasteType, ct);
            await SyncStaffMealEmployeesAsync(log, request, wasteType, ct);

            if (log.Status != WasteLogStatus.Approved || !log.IsAffectingInventory)
                return;

            var costAmount = await DeductInventoryForApprovedWasteAsync(log, userId, updatedBy, request.Notes, ct);
            log.CostAmount = costAmount > 0 ? costAmount : log.CostAmount;
        }

        private async Task UpdateSourceLinkedWasteAsync(
            WasteLog log,
            WasteLogUpdateDto request,
            CancellationToken ct)
        {
            WasteLogValidator.ValidateSourceLinkedUpdate(request, log.Category);

            log.WasteDate = NormalizeWasteDate(request.WasteDate) ?? log.WasteDate ?? log.CreatedAt;
            log.Reason = request.Reason.Trim();
            log.Notes = NormalizeText(request.Notes);
            log.AttachmentUrl = NormalizeText(request.AttachmentUrl);

            if (log.Category == WasteCategory.CancelProduct)
            {
                await ApplySourceLinkedProductFieldsAsync(log, request, ct);
                return;
            }

            await ApplySourceLinkedMaterialFieldsAsync(log, request, ct);
        }

        private async Task ApplySourceLinkedProductFieldsAsync(
            WasteLog log,
            WasteLogUpdateDto request,
            CancellationToken ct)
        {
            var productId = request.ProductId!.Value;
            var product = await _repository.GetProductAsync(productId, ct)
                ?? throw new NotFoundException("Product", productId);

            log.ItemId = product.Id;
            log.ProductId = product.Id;
            log.MaterialId = null;
            log.ItemName = product.Name;
            log.ItemNameAr = product.NameAr;
            log.Quantity = request.Quantity;
            log.Unit = NormalizeUnit(request.Unit, string.IsNullOrWhiteSpace(log.Unit) ? ProductUnit : log.Unit);
            log.CostAmount = (product.CostPrice ?? 0m) * request.Quantity;
            log.SalePriceLoss = request.SalePriceLoss ?? ResolveSalePrice(product) * request.Quantity;
        }

        private async Task ApplySourceLinkedMaterialFieldsAsync(
            WasteLog log,
            WasteLogUpdateDto request,
            CancellationToken ct)
        {
            var materialId = request.MaterialId!.Value;
            var material = await _repository.GetMaterialAsync(materialId, ct)
                ?? throw new NotFoundException("Raw material", materialId);

            var quantity = InventoryUnitConverter.Convert(request.Quantity, request.Unit, material.Unit);
            log.ItemId = material.Id;
            log.ProductId = null;
            log.MaterialId = material.Id;
            log.ItemName = material.Name;
            log.ItemNameAr = material.NameAr;
            log.Quantity = quantity;
            log.Unit = material.Unit.ToString();
            log.CostAmount = material.CostPerUnit * quantity;
            log.SalePriceLoss = request.SalePriceLoss ?? 0m;
        }

        private static void ApplyBaseEditableFields(
            WasteLog log,
            WasteLogUpdateDto request,
            ManualWasteType wasteType)
        {
            log.WasteType = wasteType;
            log.WasteDate = NormalizeWasteDate(request.WasteDate) ?? log.WasteDate ?? log.CreatedAt;
            log.Reason = request.Reason.Trim();
            log.Notes = NormalizeText(request.Notes);
            log.OrderId = request.OrderId;
            log.SourceOrderId = request.OrderId;
            log.AttachmentUrl = NormalizeText(request.AttachmentUrl);
            log.IsAffectingInventory = request.IsAffectingInventory;
        }

        private async Task ApplyProductWasteFieldsAsync(
            WasteLog log,
            WasteLogUpdateDto request,
            CancellationToken ct)
        {
            var productId = request.ProductId!.Value;
            var product = await _repository.GetProductAsync(productId, ct)
                ?? throw new NotFoundException("Product", productId);

            log.Category = WasteCategory.ManualProduct;
            log.ItemId = product.Id;
            log.ProductId = product.Id;
            log.MaterialId = null;
            log.ItemName = product.Name;
            log.ItemNameAr = product.NameAr;
            log.Quantity = request.Quantity;
            log.Unit = NormalizeUnit(request.Unit, ProductUnit);
            log.CostAmount = (product.CostPrice ?? 0m) * request.Quantity;
            log.SalePriceLoss = request.SalePriceLoss ?? ResolveSalePrice(product) * request.Quantity;
        }

        private async Task ApplyMaterialWasteFieldsAsync(
            WasteLog log,
            WasteLogUpdateDto request,
            CancellationToken ct)
        {
            var materialId = request.MaterialId!.Value;
            var material = await _repository.GetMaterialAsync(materialId, ct)
                ?? throw new NotFoundException("Raw material", materialId);

            var quantity = InventoryUnitConverter.Convert(request.Quantity, request.Unit, material.Unit);
            log.Category = WasteCategory.ManualMaterial;
            log.ItemId = material.Id;
            log.ProductId = null;
            log.MaterialId = material.Id;
            log.ItemName = material.Name;
            log.ItemNameAr = material.NameAr;
            log.Quantity = quantity;
            log.Unit = material.Unit.ToString();
            log.CostAmount = material.CostPerUnit * quantity;
            log.SalePriceLoss = request.SalePriceLoss ?? 0m;
        }

        private async Task<decimal> DeductInventoryForApprovedWasteAsync(
            WasteLog log,
            Guid? userId,
            string? userName,
            string? notes,
            CancellationToken ct)
        {
            if (log.MaterialId.HasValue)
            {
                if (!await HasSufficientStockAsync(log.MaterialId.Value, log.BranchId, log.Quantity, ct))
                    return SkipInventoryDeduction(log, "material", log.ItemName);

                var cost = await _inventoryService.DeductRawMaterialStockAsync(log.MaterialId.Value, log.Quantity);
                await AddInventoryTransactionAsync(log, log.MaterialId.Value, null, log.Quantity, log.Unit, cost, userId, userName, notes, ct);
                return cost;
            }

            if (!log.ProductId.HasValue)
                return 0m;

            var requirements = await _repository.GetProductRecipeRequirementsAsync(log.ProductId.Value, log.Quantity, ct);
            if (requirements.Count == 0)
                throw new ValidationException($"Product {log.ItemName} has no recipe stock mapping.");

            // All-or-nothing on the recipe: if any ingredient lacks stock, record the waste without
            // touching FIFO. Avoids partial deductions that would leave the order item in an
            // inconsistent state across ingredients.
            foreach (var requirement in requirements)
            {
                if (!await HasSufficientStockAsync(requirement.MaterialId, log.BranchId, requirement.Quantity, ct))
                    return SkipInventoryDeduction(log, "ingredient", requirement.MaterialName);
            }

            decimal totalCost = 0m;
            foreach (var requirement in requirements)
            {
                var cost = await _inventoryService.DeductRawMaterialStockAsync(requirement.MaterialId, requirement.Quantity);
                totalCost += cost;
                await AddInventoryTransactionAsync(log, requirement.MaterialId, log.ProductId, requirement.Quantity, requirement.Unit, cost, userId, userName, notes, ct);
            }

            return totalCost;
        }

        private async Task<bool> HasSufficientStockAsync(Guid materialId, Guid branchId, decimal quantity, CancellationToken ct)
        {
            var context = await _branchContext.GetCurrentAsync(ct);
            var allowLegacyFallback = context.CurrentBranch.Id == branchId && context.CurrentBranch.IsMainBranch;
            var available = await _repository.GetAvailableStockAsync(materialId, branchId, allowLegacyFallback, ct);
            return available >= quantity;
        }

        // Caller asked to record the waste even when stock is empty/insufficient.
        // The waste row is kept (for audit), the inventory side is skipped, and CostAmount falls
        // back to the estimated cost set in BuildProductWasteAsync / BuildMaterialWasteAsync.
        private decimal SkipInventoryDeduction(WasteLog log, string kind, string name)
        {
            _logger.LogWarning(
                "Waste log {WasteLogId} ({WasteNumber}) saved without inventory impact — {Kind} {Name} has insufficient stock for quantity {Quantity}.",
                log.Id, log.WasteNumber, kind, name, log.Quantity);

            log.IsAffectingInventory = false;
            return log.CostAmount;
        }

        private async Task AddInventoryTransactionAsync(
            WasteLog log,
            Guid? materialId,
            Guid? productId,
            decimal quantity,
            string unit,
            decimal cost,
            Guid? userId,
            string? userName,
            string? notes,
            CancellationToken ct,
            InventoryTransactionType transactionType = InventoryTransactionType.Waste)
        {
            await _repository.AddInventoryTransactionAsync(new InventoryTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = log.TenantId,
                BranchId = log.BranchId,
                TransactionType = transactionType,
                WasteLogId = log.Id,
                ProductId = productId,
                MaterialId = materialId,
                Quantity = quantity,
                Unit = unit,
                CostAmount = cost,
                ReferenceNumber = log.WasteNumber,
                CreatedById = userId,
                CreatedBy = userName,
                Notes = NormalizeText(notes)
            }, ct);
        }

        private async Task ReverseCurrentInventoryImpactAsync(
            WasteLog log,
            Guid? userId,
            string? userName,
            CancellationToken ct)
        {
            var impacts = log.InventoryTransactions
                .Where(t => t.MaterialId.HasValue)
                .GroupBy(t => new { t.MaterialId, t.ProductId, t.Unit })
                .Select(g => new
                {
                    g.Key.MaterialId,
                    g.Key.ProductId,
                    g.Key.Unit,
                    Quantity = g.Sum(t => t.Quantity),
                    Cost = g.Sum(t => t.CostAmount)
                })
                .Where(x => x.Quantity > 0)
                .ToList();

            foreach (var impact in impacts)
            {
                await _inventoryService.RestoreRawMaterialStockAsync(impact.MaterialId!.Value, impact.Quantity, ct);
                await AddInventoryTransactionAsync(
                    log,
                    impact.MaterialId,
                    impact.ProductId,
                    -impact.Quantity,
                    impact.Unit,
                    -impact.Cost,
                    userId,
                    userName,
                    WasteEditReversalNote,
                    ct,
                    InventoryTransactionType.WasteReversal);
            }
        }

        private async Task ApplyStaffMealEmployeesAsync(
            WasteLog log,
            WasteLogCreateDto request,
            ManualWasteType wasteType,
            CancellationToken ct)
        {
            if (wasteType != ManualWasteType.STAFF_MEAL)
                return;

            var ids = (request.EmployeeIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
                return; // validator already rejects this case

            var employees = await _repository.GetEmployeeOptionsAsync(ids, ct);
            if (employees.Count != ids.Count)
            {
                var missing = ids.Except(employees.Select(e => e.Id)).First();
                throw new NotFoundException("Employee", missing);
            }

            foreach (var employee in employees)
            {
                log.Employees.Add(new WasteLogEmployee
                {
                    Id = Guid.NewGuid(),
                    TenantId = log.TenantId,
                    WasteLogId = log.Id,
                    EmployeeId = employee.Id,
                    EmployeeNameSnapshot = employee.Name
                });
            }
        }

        private async Task SyncStaffMealEmployeesAsync(
            WasteLog log,
            WasteLogUpdateDto request,
            ManualWasteType wasteType,
            CancellationToken ct)
        {
            if (wasteType != ManualWasteType.STAFF_MEAL)
            {
                log.Employees.Clear();
                return;
            }

            var ids = (request.EmployeeIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var employees = await _repository.GetEmployeeOptionsAsync(ids, ct);
            if (employees.Count != ids.Count)
            {
                var missing = ids.Except(employees.Select(e => e.Id)).First();
                throw new NotFoundException("Employee", missing);
            }

            var employeeLookup = employees.ToDictionary(e => e.Id);
            var remove = log.Employees
                .Where(e => !e.EmployeeId.HasValue || !employeeLookup.ContainsKey(e.EmployeeId.Value))
                .ToList();

            foreach (var employee in remove)
                log.Employees.Remove(employee);

            foreach (var employee in employees)
                UpsertWasteEmployee(log, employee);
        }

        private static void UpsertWasteEmployee(WasteLog log, WasteEmployeeOptionDto employee)
        {
            var existing = log.Employees.FirstOrDefault(e => e.EmployeeId == employee.Id);
            if (existing != null)
            {
                existing.EmployeeNameSnapshot = employee.Name;
                return;
            }

            log.Employees.Add(new WasteLogEmployee
            {
                Id = Guid.NewGuid(),
                TenantId = log.TenantId,
                WasteLogId = log.Id,
                EmployeeId = employee.Id,
                EmployeeNameSnapshot = employee.Name
            });
        }

        private async Task ValidateOrderAsync(Guid? orderId, Guid branchId, CancellationToken ct)
        {
            if (!orderId.HasValue)
                return;

            var order = await _repository.GetOrderAsync(orderId.Value, ct)
                ?? throw new NotFoundException("Order", orderId.Value);
            if (order.BranchId != branchId)
                throw new NotFoundException("Order", orderId.Value);
        }

        private async Task EnsureCurrentBranchAsync(WasteLog log, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            if (log.BranchId != branchId)
                throw new NotFoundException("Waste log", log.Id);
        }

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
            => (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

        private async Task<Guid?> ResolveReadBranchIdAsync(Guid? requestedBranchId, CancellationToken ct)
        {
            if (requestedBranchId == Guid.Empty)
            {
                if (!_currentUser.IsAdminOrManager)
                    throw new ForbiddenException("All branches waste-log scope requires administrator or manager access.");
                return null;
            }

            var context = await _branchContext.GetCurrentAsync(ct);
            if (!requestedBranchId.HasValue)
                return context.CurrentBranch.Id;

            var selected = context.AssignedBranches.FirstOrDefault(b => b.Id == requestedBranchId.Value);
            if (selected is null)
                throw new ForbiddenException("You are not assigned to the selected branch.");
            if (!selected.IsActive)
                throw new ValidationException("Inactive branches cannot be used as waste-log scope.");

            return requestedBranchId.Value;
        }

        private static WasteLogAudit BuildAudit(
            WasteLog log,
            WasteLogAuditAction action,
            WasteLogStatus? fromStatus,
            WasteLogStatus toStatus,
            Guid? userId,
            string? userName,
            string? notes)
        {
            return new WasteLogAudit
            {
                Id = Guid.NewGuid(),
                TenantId = log.TenantId,
                WasteLogId = log.Id,
                Action = action,
                PerformedById = userId,
                PerformedByName = userName,
                FromStatus = fromStatus?.ToString(),
                ToStatus = toStatus.ToString(),
                Notes = NormalizeText(notes)
            };
        }

        private static WasteLogAudit BuildUpdateAudit(
            WasteLog log,
            IReadOnlyDictionary<string, object?> previousValues,
            IReadOnlyDictionary<string, object?> newValues,
            Guid? userId,
            string? userName)
        {
            var changedFields = previousValues.Keys
                .Where(key => SerializeAuditValue(previousValues[key]) != SerializeAuditValue(newValues[key]))
                .ToList();

            return new WasteLogAudit
            {
                Id = Guid.NewGuid(),
                TenantId = log.TenantId,
                WasteLogId = log.Id,
                Action = WasteLogAuditAction.Updated,
                PerformedById = userId,
                PerformedByName = userName,
                FromStatus = log.Status.ToString(),
                ToStatus = log.Status.ToString(),
                Notes = changedFields.Count == 0 ? "No field changes." : $"Changed: {string.Join(", ", changedFields)}",
                ChangedFields = string.Join(", ", changedFields),
                PreviousValues = JsonSerializer.Serialize(previousValues, AuditJsonOptions),
                NewValues = JsonSerializer.Serialize(newValues, AuditJsonOptions)
            };
        }

        private static IReadOnlyDictionary<string, object?> BuildAuditSnapshot(WasteLog log)
        {
            return new Dictionary<string, object?>
            {
                ["WasteDate"] = log.WasteDate ?? log.CreatedAt,
                ["WasteType"] = log.WasteType?.ToString(),
                ["Category"] = log.Category.ToString(),
                ["ProductId"] = log.ProductId,
                ["MaterialId"] = log.MaterialId,
                ["ItemName"] = log.ItemName,
                ["ItemNameAr"] = log.ItemNameAr,
                ["Quantity"] = log.Quantity,
                ["Unit"] = log.Unit,
                ["Reason"] = log.Reason,
                ["Notes"] = log.Notes,
                ["EmployeeIds"] = log.Employees
                    .Where(e => e.EmployeeId.HasValue)
                    .Select(e => e.EmployeeId!.Value)
                    .OrderBy(id => id)
                    .ToList(),
                ["Employees"] = log.Employees
                    .OrderBy(e => e.EmployeeNameSnapshot)
                    .Select(e => e.EmployeeNameSnapshot)
                    .ToList(),
                ["SalePriceLoss"] = log.SalePriceLoss,
                ["CostAmount"] = log.CostAmount,
                ["BranchId"] = log.BranchId,
                ["OrderId"] = log.OrderId ?? log.SourceOrderId,
                ["AttachmentUrl"] = log.AttachmentUrl,
                ["IsAffectingInventory"] = log.IsAffectingInventory,
                ["Status"] = log.Status.ToString()
            };
        }

        private static WasteLogDto MapToDto(WasteLog log, bool includeAudit)
        {
            var costAmount = ResolveCostAmount(log);
            var salePriceLoss = ResolveSalePriceLoss(log);

            return new WasteLogDto
            {
                Id = log.Id,
                WasteNumber = log.WasteNumber,
                WasteType = log.WasteType?.ToString(),
                Category = log.Category.ToString(),
                WasteCategory = log.Category.ToString(),
                ItemId = log.ItemId,
                ProductId = log.ProductId,
                MaterialId = log.MaterialId,
                ItemName = log.ItemName,
                ItemNameAr = log.ItemNameAr
                    ?? log.Product?.NameAr
                    ?? log.Material?.NameAr
                    ?? log.SourceOrderItem?.SelectedOptionNameAr
                    ?? log.SourceOrderItem?.Product?.NameAr,
                Quantity = log.Quantity,
                Unit = log.Unit,
                Reason = ResolveReason(log.Reason, preferArabic: false),
                ReasonAr = ResolveReason(log.Reason, preferArabic: true),
                Notes = log.Notes,
                CostAmount = costAmount,
                SalePriceLoss = salePriceLoss,
                Status = log.Status.ToString(),
                CreatedById = log.LoggedById,
                CreatedBy = log.CreatedBy ?? log.LoggedByName,
                LoggedBy = log.LoggedByName ?? "System",
                Employees = log.Employees
                    .OrderBy(e => e.EmployeeNameSnapshot)
                    .Select(e => new WasteParticipantDto
                    {
                        EmployeeId = e.EmployeeId,
                        Name = e.EmployeeNameSnapshot,
                        NameAr = e.Employee?.FullNameAr
                    })
                    .ToList(),
                ApprovedById = log.ApprovedById,
                ApprovedBy = log.ApprovedBy,
                DecisionAt = log.DecisionAt,
                BranchId = log.BranchId,
                OrderId = log.OrderId ?? log.SourceOrderId,
                SourceOrderId = log.SourceOrderId,
                OrderNumber = log.SourceOrder?.OrderNumber,
                SourceBatchId = log.SourceBatchId,
                BatchNumber = log.SourceBatchNumber ?? log.SourceBatch?.BatchNumber,
                AttachmentUrl = log.AttachmentUrl,
                IsAffectingInventory = ResolveInventoryImpact(log),
                WasteDate = log.WasteDate ?? log.CreatedAt,
                CreatedAt = log.CreatedAt,
                UpdatedAt = log.UpdatedAt,
                AuditTrail = includeAudit
                    ? log.AuditTrail
                        .OrderBy(a => a.CreatedAt)
                        .Select(a => new WasteLogAuditDto
                        {
                            Id = a.Id,
                            Action = a.Action.ToString(),
                            PerformedById = a.PerformedById,
                            PerformedByName = a.PerformedByName,
                            FromStatus = a.FromStatus,
                            ToStatus = a.ToStatus,
                            Notes = a.Notes,
                            ChangedFields = a.ChangedFields,
                            PreviousValues = a.PreviousValues,
                            NewValues = a.NewValues,
                            CreatedAt = a.CreatedAt
                        })
                        .ToList()
                    : new List<WasteLogAuditDto>()
            };
        }

        private static decimal ResolveCostAmount(WasteLog log)
        {
            if (log.CostAmount > 0m)
                return log.CostAmount;

            var transactionCost = log.InventoryTransactions.Sum(t => t.CostAmount);
            if (transactionCost > 0m)
                return transactionCost;

            if (log.Category == WasteCategory.ExpiryProduct && log.SourceBatch != null)
                return log.Quantity * log.SourceBatch.UnitCost;

            if (log.Category == WasteCategory.CancelProduct && log.SourceOrderItem != null)
                return log.SourceOrderItem.StockDeductedCost;

            if ((log.Category == WasteCategory.ManualProduct || log.Category == WasteCategory.ManualMaterial) && log.Amount > 0m)
                return log.Amount;

            return 0m;
        }

        private static decimal ResolveSalePriceLoss(WasteLog log)
        {
            if (log.SalePriceLoss > 0m)
                return log.SalePriceLoss;

            return log.Category == WasteCategory.CancelProduct && log.Amount > 0m ? log.Amount : 0m;
        }

        private static bool ResolveInventoryImpact(WasteLog log)
            => log.IsAffectingInventory
            || log.InventoryTransactions.Sum(t => t.Quantity) > 0m
            || (log.Category == WasteCategory.ExpiryProduct && (log.SourceBatchId.HasValue || log.SourceBatch != null));

        private static bool IsManualWaste(WasteLog log)
            => log.Category == WasteCategory.ManualProduct || log.Category == WasteCategory.ManualMaterial;

        private static string ResolveReason(string reason, bool preferArabic)
        {
            var parts = reason.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
                return reason;

            return preferArabic ? parts[1] : parts[0];
        }

        private static string GenerateWasteNumber()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            return $"WL-{DateTime.UtcNow:yyyyMMdd}-{suffix}";
        }

        private static decimal ResolveSalePrice(Product product)
            => product.DiscountedPrice ?? product.BasePrice;

        private static WasteLogQueryDto EnsureSummaryDateRange(WasteLogQueryDto query)
        {
            if (query.DateFrom.HasValue || query.DateTo.HasValue)
                return query;

            var todayStart = DateTime.UtcNow.Date;
            return new WasteLogQueryDto
            {
                Category = query.Category,
                WasteType = query.WasteType,
                Status = query.Status,
                EmployeeId = query.EmployeeId,
                StaffMealEmployeeId = query.StaffMealEmployeeId,
                ProductId = query.ProductId,
                MaterialId = query.MaterialId,
                BranchId = query.BranchId,
                DateFrom = todayStart,
                DateTo = todayStart,
                Page = query.Page,
                Limit = query.Limit
            };
        }

        private static string NormalizeUnit(string? requestedUnit, string fallback)
            => string.IsNullOrWhiteSpace(requestedUnit) ? fallback : requestedUnit.Trim();

        private static string? NormalizeName(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? NormalizeText(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static DateTime? NormalizeWasteDate(DateTime? value)
            => value?.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
                _ => value
            };

        private static string SerializeAuditValue(object? value)
            => JsonSerializer.Serialize(value, AuditJsonOptions);

        private static string CsvEscape(string? value)
        {
            if (value == null) return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
