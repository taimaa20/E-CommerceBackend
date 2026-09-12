using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services;

public sealed class SimplePurchaseService : ISimplePurchaseService
{
    private const int UnitCostDecimalPlaces = 3;
    private const int QuantityDecimalPlaces = 2;

    private readonly IProcurementRepository _repository;
    private readonly IExpenseInvoiceService _invoiceService;
    private readonly IPurchaseOrderWorkflowService _workflow;
    private readonly ILogger<SimplePurchaseService> _logger;

    public SimplePurchaseService(
        IProcurementRepository repository,
        IExpenseInvoiceService invoiceService,
        IPurchaseOrderWorkflowService workflow,
        ILogger<SimplePurchaseService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SimplePurchaseDetailsDto> CreateAsync(
        SimplePurchaseCreateDto dto,
        Guid? userId,
        string? userName,
        bool isArabic,
        CancellationToken ct)
    {
        Validate(dto);
        var scope = await _repository.GetCurrentScopeAsync(ct);
        await ValidateReferencesAsync(dto, ct);

        var order = BuildOrder(dto, scope);
        ExpenseInvoiceDetailsDto? invoice = null;

        await using (var transaction = await _repository.BeginTransactionAsync(ct))
        {
            await _repository.AddOrderAsync(order, ct);
            await _repository.SaveChangesAsync(ct);
            if (!string.IsNullOrWhiteSpace(dto.InvoiceNumber))
            {
                invoice = await _invoiceService.CreateAsync(
                    BuildInvoice(dto, order),
                    userId,
                    userName,
                    isArabic,
                    ct);
            }

            if (dto.ReceiveIntoInventory)
                await ReceiveOrderAsync(order.Id, ct);

            await transaction.CommitAsync(ct);
        }

        _logger.LogInformation(
            "Simple purchase {PurchaseOrderId} created with invoice {InvoiceId}; receive={Receive}",
            order.Id,
            invoice?.Id,
            dto.ReceiveIntoInventory);

        return await GetDetailsAsync(order.Id, isArabic, ct);
    }

    public async Task<SimplePurchaseDetailsDto> GetDetailsAsync(
        Guid purchaseOrderId,
        bool isArabic,
        CancellationToken ct)
        => await _repository.GetSimplePurchaseDetailsAsync(purchaseOrderId, isArabic, ct)
            ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

    public async Task<SimplePurchaseDetailsDto> ReceiveAsync(
        Guid purchaseOrderId,
        bool isArabic,
        CancellationToken ct)
    {
        await ReceiveOrderAsync(purchaseOrderId, ct);
        return await GetDetailsAsync(purchaseOrderId, isArabic, ct);
    }

    public async Task<SimplePurchaseDetailsDto> ApproveAsync(
        Guid purchaseOrderId,
        bool isArabic,
        CancellationToken ct)
    {
        await _workflow.UpdateStatusAsync(
            purchaseOrderId,
            PurchaseOrderStatus.Approved,
            ct);
        return await GetDetailsAsync(purchaseOrderId, isArabic, ct);
    }

    private async Task ReceiveOrderAsync(Guid purchaseOrderId, CancellationToken ct)
    {
        var order = await _repository.GetOrderAsync(purchaseOrderId, ct)
            ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

        if (order.Status == PurchaseOrderStatus.Approved)
            throw new ValidationException("This purchase has already been approved.");

        if (order.Status == PurchaseOrderStatus.Received)
            throw new ValidationException("This purchase is already received and awaiting approval.");

        if (order.Status == PurchaseOrderStatus.Cancelled)
            throw new ValidationException("A cancelled purchase cannot be received.");

        if (order.Status == PurchaseOrderStatus.Draft)
            await _workflow.UpdateStatusAsync(order.Id, PurchaseOrderStatus.Received, ct);
    }

    private async Task ValidateReferencesAsync(SimplePurchaseCreateDto dto, CancellationToken ct)
    {
        if (!await _repository.SupplierExistsAsync(dto.SupplierId, ct))
            throw new ValidationException("The selected supplier was not found.");

        var materialIds = dto.Items.Select(item => item.RawMaterialId).Distinct().ToList();
        var materials = await _repository.GetMaterialsAsync(materialIds, ct);
        if (materials.Count != materialIds.Count)
            throw new ValidationException("One or more selected items were not found.");
    }

    private static PurchaseOrder BuildOrder(
        SimplePurchaseCreateDto dto,
        ProcurementScope scope)
    {
        var orderId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var order = new PurchaseOrder
        {
            Id = orderId,
            TenantId = scope.TenantId,
            BranchId = scope.BranchId,
            SupplierId = dto.SupplierId,
            OrderNumber = $"PO-{createdAt:yyyyMMdd}-{orderId.ToString("N")[..8]}",
            InvoiceNumber = TrimToNull(dto.InvoiceNumber),
            Status = PurchaseOrderStatus.Draft,
            ExpectedDate = EnsureUtc(dto.InvoiceDate),
            CreatedAt = createdAt,
            Items = dto.Items.Select(item => new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = scope.TenantId,
                BranchId = scope.BranchId,
                PurchaseOrderId = orderId,
                RawMaterialId = item.RawMaterialId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitCost,
                CreatedAt = createdAt
            }).ToList()
        };

        order.ItemCount = (int)order.Items.Sum(item => item.Quantity);
        order.UnitCost = order.Items.First().UnitPrice;
        order.TotalAmount = order.Items.Sum(item => item.Quantity * item.UnitPrice);
        return order;
    }

    private static CreateExpenseInvoiceDto BuildInvoice(
        SimplePurchaseCreateDto dto,
        PurchaseOrder order)
    {
        var subtotal = decimal.Round(order.TotalAmount, 2, MidpointRounding.AwayFromZero);
        var total = decimal.Round(
            subtotal + dto.TaxAmount - dto.DiscountAmount,
            2,
            MidpointRounding.AwayFromZero);

        return new CreateExpenseInvoiceDto
        {
            InvoiceNumber = dto.InvoiceNumber!.Trim(),
            SupplierId = dto.SupplierId,
            InvoiceDate = EnsureUtc(dto.InvoiceDate),
            DueDate = dto.DueDate.HasValue ? EnsureUtc(dto.DueDate.Value) : null,
            Subtotal = subtotal,
            TaxAmount = dto.TaxAmount,
            DiscountAmount = dto.DiscountAmount,
            TotalAmount = total,
            PaidAmount = dto.PaidAmount,
            PurchaseOrderId = order.Id,
            CurrencyCode = dto.CurrencyCode,
            PaymentMethod = dto.PaymentMethod,
            PaymentMethodDetail = dto.PaymentMethodDetail,
            Notes = dto.Notes,
            Status = "Unpaid",
            AcknowledgeDuplicateWarning = dto.AcknowledgeDuplicateWarning
        };
    }

    private static void Validate(SimplePurchaseCreateDto dto)
    {
        if (dto.SupplierId == Guid.Empty)
            throw new ValidationException("Supplier is required.");

        if (dto.DueDate.HasValue && dto.DueDate.Value.Date < dto.InvoiceDate.Date)
            throw new ValidationException("Invoice due date cannot be before the purchase date.");

        if (dto.Items.Count == 0)
            throw new ValidationException("At least one item is required.");

        if (dto.Items.Select(item => item.RawMaterialId).Distinct().Count() != dto.Items.Count)
            throw new ValidationException("The same item cannot be added more than once.");

        foreach (var item in dto.Items)
        {
            if (item.RawMaterialId == Guid.Empty || item.Quantity <= 0 || item.UnitCost <= 0)
                throw new ValidationException("Every item needs a quantity and unit cost greater than zero.");

            if (Scale(item.Quantity) > QuantityDecimalPlaces)
                throw new ValidationException("Item quantity must have no more than 2 decimal places.");

            if (Scale(item.UnitCost) > UnitCostDecimalPlaces)
                throw new ValidationException("Item unit cost must have no more than 3 decimal places.");
        }

        var subtotal = decimal.Round(
            dto.Items.Sum(item => item.Quantity * item.UnitCost),
            2,
            MidpointRounding.AwayFromZero);
        var total = subtotal + dto.TaxAmount - dto.DiscountAmount;

        if (total < 0)
            throw new ValidationException("Discount cannot exceed subtotal plus tax.");

        if (Scale(dto.TaxAmount) > 2 || Scale(dto.DiscountAmount) > 2 || Scale(dto.PaidAmount) > 2)
            throw new ValidationException("Tax, discount and paid amount must have no more than 2 decimal places.");

        if (string.IsNullOrWhiteSpace(dto.InvoiceNumber)
            && (dto.TaxAmount > 0 || dto.DiscountAmount > 0 || dto.PaidAmount > 0))
            throw new ValidationException(
                "Invoice number is required when recording tax, discount or payment.");

        if (string.IsNullOrWhiteSpace(dto.InvoiceNumber)
            && (dto.DueDate.HasValue
                || !string.IsNullOrWhiteSpace(dto.Notes)
                || !string.IsNullOrWhiteSpace(dto.PaymentMethodDetail)))
            throw new ValidationException(
                "Invoice number is required when recording invoice details.");

        if (dto.PaidAmount > 0
            && (string.IsNullOrWhiteSpace(dto.PaymentMethod)
                || dto.PaymentMethod == "Unspecified"))
            throw new ValidationException("Payment method is required when recording a payment.");

        ExpenseInvoicePaymentPolicy.ResolvePaidAmount(total, dto.PaidAmount, ExpenseInvoiceStatus.Unpaid);
    }

    private static int Scale(decimal value)
        => (decimal.GetBits(value)[3] >> 16) & 0x7F;

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
