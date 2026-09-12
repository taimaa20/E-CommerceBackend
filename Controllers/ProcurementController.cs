using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using System.Security.Claims;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.ProcurementOperators)]
    public class ProcurementController : ControllerBase
    {
        private const int PurchaseUnitPriceDecimalPlaces = 3;
        private const string UnitPricePrecisionError = "Item unit price must have no more than 3 decimal places.";

        private readonly PosDbContext _context;
        private readonly IProcurementService _procurementService;
        private readonly IBranchContext _branchContext;
        private readonly ITenantResolver _tenantResolver;
        private readonly IPurchaseOrderWorkflowService _workflowService;
        private readonly ISimplePurchaseService _simplePurchaseService;

        public ProcurementController(
            PosDbContext context,
            IProcurementService procurementService,
            IBranchContext branchContext,
            ITenantResolver tenantResolver,
            IPurchaseOrderWorkflowService workflowService,
            ISimplePurchaseService simplePurchaseService)
        {
            _context = context;
            _procurementService = procurementService;
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _simplePurchaseService = simplePurchaseService ?? throw new ArgumentNullException(nameof(simplePurchaseService));
        }

        // --- Suggestions ---
        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestions()
        {
            var suggestions = await _procurementService.GetSuggestedOrdersAsync();
            var isArabic = IsArabicRequested(Request);

            if (isArabic && suggestions.Count > 0)
            {
                var materialIds = suggestions.Select(s => s.RawMaterialId).Distinct().ToList();
                var nameArMap = await _context.RawMaterials
                    .AsNoTracking()
                    .Where(rm => materialIds.Contains(rm.Id))
                    .Select(rm => new { rm.Id, rm.NameAr })
                    .ToDictionaryAsync(x => x.Id, x => x.NameAr);

                foreach (var suggestion in suggestions)
                {
                    if (nameArMap.TryGetValue(suggestion.RawMaterialId, out var nameAr))
                    {
                        suggestion.RawMaterialName = ResolveDisplayName(suggestion.RawMaterialName, nameAr, isArabic);
                    }
                }
            }

            return Ok(suggestions);
        }

        // --- Suppliers ---
        [HttpGet("suppliers")]
        public async Task<IActionResult> GetSuppliers()
        {
            var isArabic = IsArabicRequested(Request);
            var suppliers = await _context.Suppliers.AsNoTracking()
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.NameAr,
                    DisplayName = isArabic && s.NameAr != null && s.NameAr != string.Empty ? s.NameAr : s.Name,
                    s.Email,
                    s.MobileNumber,
                    s.ContactInfo,
                    s.LeadTimeDays,
                    s.TaxNumber,
                    s.VendorCode,
                    s.Country,
                    s.Website,
                    s.Brands,
                    s.BestSellers
                })
                .ToListAsync();
            return Ok(suppliers);
        }

        [HttpGet("suppliers/paginated")]
        public async Task<IActionResult> GetSuppliersPaginated(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string searchTerm = "",
            [FromQuery] string sortBy = "name",
            [FromQuery] string sortOrder = "asc")
        {
            var isArabic = IsArabicRequested(Request);
            var query = _context.Suppliers.AsQueryable();
            var normalizedSearchTerm = (searchTerm ?? string.Empty).Trim();

            // Search filter
            if (!string.IsNullOrWhiteSpace(normalizedSearchTerm))
            {
                query = query.Where(s =>
                    EF.Functions.ILike(s.Name, $"%{normalizedSearchTerm}%") ||
                    (s.NameAr != null && EF.Functions.ILike(s.NameAr, $"%{normalizedSearchTerm}%")) ||
                    (s.Email != null && EF.Functions.ILike(s.Email, $"%{normalizedSearchTerm}%")) ||
                    (s.MobileNumber != null && EF.Functions.ILike(s.MobileNumber, $"%{normalizedSearchTerm}%")) ||
                    (s.Country != null && EF.Functions.ILike(s.Country, $"%{normalizedSearchTerm}%")) ||
                    (s.Brands != null && EF.Functions.ILike(s.Brands, $"%{normalizedSearchTerm}%")) ||
                    (s.ContactInfo != null && EF.Functions.ILike(s.ContactInfo, $"%{normalizedSearchTerm}%")));
            }

            // Sorting
            query = sortBy.ToLower() switch
            {
                "leadtime" => sortOrder == "desc" ? query.OrderByDescending(s => s.LeadTimeDays) : query.OrderBy(s => s.LeadTimeDays),
                _ => sortOrder == "desc" ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.NameAr,
                    DisplayName = isArabic && s.NameAr != null && s.NameAr != string.Empty ? s.NameAr : s.Name,
                    s.Email,
                    s.MobileNumber,
                    s.ContactInfo,
                    s.LeadTimeDays,
                    s.TaxNumber,
                    s.VendorCode,
                    s.Country,
                    s.Website,
                    s.Brands,
                    s.BestSellers,
                    // One supplier master serves both sides of the business, so the row says how
                    // it is used rather than which screen it belongs to.
                    RetailProductCount = _context.RetailProductDetails.Count(d => d.SupplierId == s.Id),
                    PurchaseOrderCount = _context.PurchaseOrders.Count(po => po.SupplierId == s.Id),
                    RetailPurchaseOrderCount = _context.RetailPurchaseOrders.Count(po => po.SupplierId == s.Id),
                    LegacyPurchaseCount = _context.RetailLegacyPurchases.Count(lp => lp.SupplierId == s.Id)
                })
                .ToListAsync();

            return Ok(new PaginatedResponse<object>
            {
                Items = items.Cast<object>().ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        [HttpPost("suppliers")]
        public async Task<IActionResult> CreateSupplier(Supplier supplier)
        {
            // Validation: Name required
            if (string.IsNullOrWhiteSpace(supplier.Name))
                return BadRequest("Supplier name is required");

            var contactError = ValidateSupplierContact(supplier);
            if (contactError != null)
                return BadRequest(contactError);

            // Duplicate EN name check (case-insensitive)
            var duplicateEn = await _context.Suppliers
                .AnyAsync(s => s.Name.ToLower() == supplier.Name.Trim().ToLower());
            if (duplicateEn)
                return Conflict(new { message = "A supplier with this English name already exists" });

            // Duplicate AR name check (if provided)
            if (!string.IsNullOrWhiteSpace(supplier.NameAr))
            {
                var duplicateAr = await _context.Suppliers
                    .AnyAsync(s => s.NameAr != null && s.NameAr.ToLower() == supplier.NameAr.Trim().ToLower());
                if (duplicateAr)
                    return Conflict(new { message = "A supplier with this Arabic name already exists" });
            }

            supplier.Id = Guid.NewGuid();
            supplier.TenantId = _tenantResolver.GetTenantId();
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();
            var isArabic = IsArabicRequested(Request);
            return CreatedAtAction(nameof(GetSuppliers), new { id = supplier.Id }, new
            {
                supplier.Id,
                supplier.Name,
                supplier.NameAr,
                DisplayName = ResolveDisplayName(supplier.Name, supplier.NameAr, isArabic),
                supplier.Email,
                supplier.MobileNumber,
                supplier.ContactInfo,
                supplier.LeadTimeDays,
                supplier.TaxNumber,
                supplier.VendorCode,
                supplier.Country,
                supplier.Website,
                supplier.Brands,
                supplier.BestSellers
            });
        }
        // PUT: api/Procurement/suppliers/{id}
        [HttpPut("suppliers/{id}")]
 
        public async Task<IActionResult> UpdateSupplier(Guid id, Supplier supplier)
        {
            var existingSupplier = await _context.Suppliers.FindAsync(id);
            if (existingSupplier == null)
            {
                return NotFound();
            }

            // Validation: Name required
            if (string.IsNullOrWhiteSpace(supplier.Name))
                return BadRequest("Supplier name is required");

            var contactError = ValidateSupplierContact(supplier);
            if (contactError != null)
                return BadRequest(contactError);

            // Duplicate EN name check (exclude self)
            var duplicateEn = await _context.Suppliers
                .AnyAsync(s => s.Id != id && s.Name.ToLower() == supplier.Name.Trim().ToLower());
            if (duplicateEn)
                return Conflict(new { message = "A supplier with this English name already exists" });

            // Duplicate AR name check (if provided, exclude self)
            if (!string.IsNullOrWhiteSpace(supplier.NameAr))
            {
                var duplicateAr = await _context.Suppliers
                    .AnyAsync(s => s.Id != id && s.NameAr != null && s.NameAr.ToLower() == supplier.NameAr.Trim().ToLower());
                if (duplicateAr)
                    return Conflict(new { message = "A supplier with this Arabic name already exists" });
            }

            // Update properties
            existingSupplier.Name = supplier.Name;
            existingSupplier.NameAr = supplier.NameAr;
            existingSupplier.Email = supplier.Email;
            existingSupplier.MobileNumber = supplier.MobileNumber;
            existingSupplier.TaxNumber = supplier.TaxNumber;
            existingSupplier.VendorCode = supplier.VendorCode;
            existingSupplier.ContactInfo = supplier.ContactInfo;
            existingSupplier.LeadTimeDays = supplier.LeadTimeDays;
            existingSupplier.Country = supplier.Country;
            existingSupplier.Website = supplier.Website;
            existingSupplier.Brands = supplier.Brands;
            existingSupplier.BestSellers = supplier.BestSellers;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SupplierExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/Procurement/suppliers/{id}
        [HttpDelete("suppliers/{id}")]
   
        public async Task<IActionResult> DeleteSupplier(Guid id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }

            // One master, so every user of it has to be checked — deleting a supplier that a
            // retail product, purchase order or imported purchase still points at would break
            // those records.
            if (await _context.PurchaseOrders.AnyAsync(po => po.SupplierId == id))
                return BadRequest("Cannot delete supplier with existing purchase orders");

            if (await _context.RetailPurchaseOrders.AnyAsync(po => po.SupplierId == id))
                return BadRequest("Cannot delete supplier with existing retail purchase orders");

            if (await _context.RetailLegacyPurchases.AnyAsync(lp => lp.SupplierId == id))
                return BadRequest("Cannot delete supplier with imported purchase history");

            if (await _context.RetailProductDetails.AnyAsync(d => d.SupplierId == id))
                return BadRequest("Cannot delete supplier while retail products are sourced from it");

            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Procurement/suppliers/{id}
        [HttpGet("suppliers/{id}")]
        public async Task<IActionResult> GetSupplier(Guid id)
        {
            var isArabic = IsArabicRequested(Request);
            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                supplier.Id,
                supplier.Name,
                supplier.NameAr,
                DisplayName = ResolveDisplayName(supplier.Name, supplier.NameAr, isArabic),
                supplier.Email,
                supplier.MobileNumber,
                supplier.ContactInfo,
                supplier.LeadTimeDays,
                supplier.TaxNumber,
                supplier.VendorCode,
                supplier.Country,
                supplier.Website,
                supplier.Brands,
                supplier.BestSellers
            });
        }

        /// <summary>
        /// One supplier master serves restaurant procurement and retail, and the two reach their
        /// suppliers differently: a local food supplier has a mobile number, an overseas brand
        /// distributor has a wholesale email and a website. The rule is therefore "at least one
        /// way to reach them", with each contact validated for format when it is supplied.
        /// Every supplier that satisfied the previous mobile-only rule still satisfies this one.
        /// </summary>
        private static string? ValidateSupplierContact(Supplier supplier)
        {
            var hasMobile = !string.IsNullOrWhiteSpace(supplier.MobileNumber);
            var hasEmail = !string.IsNullOrWhiteSpace(supplier.Email);
            var hasWebsite = !string.IsNullOrWhiteSpace(supplier.Website);

            if (!hasMobile && !hasEmail && !hasWebsite)
                return "A supplier needs at least one contact: mobile number, email or website";

            if (hasMobile &&
                !System.Text.RegularExpressions.Regex.IsMatch(supplier.MobileNumber!, @"^\+?[0-9]{7,15}$"))
            {
                return "Mobile number must contain 7-15 digits with optional '+' prefix";
            }

            if (!hasEmail)
                return null;

            try
            {
                _ = new System.Net.Mail.MailAddress(supplier.Email!);
            }
            catch
            {
                return "Invalid email format";
            }

            return null;
        }

        private bool SupplierExists(Guid id)
        {
            return _context.Suppliers.Any(s => s.Id == id);
        }
        // --- Purchase Orders ---
        [HttpGet("orders")]
        public async Task<IActionResult> GetPurchaseOrders(CancellationToken ct)
        {
            var isArabic = IsArabicRequested(Request);
            var branchId = await GetCurrentBranchIdAsync(ct);
            var orders = await _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.SupplierInvoice)
                .Include(po => po.Items)
                    .ThenInclude(i => i.RawMaterial)
                .Where(po => po.BranchId == branchId)
                .OrderByDescending(po => po.CreatedAt)
                .ToListAsync(ct);
            
            // Map to clean response without circular references
            var response = orders.Select(po => new
            {
                po.Id,
                po.OrderNumber,
                po.SupplierId,
                Supplier = new
                {
                    po.Supplier.Id,
                    po.Supplier.Name,
                    po.Supplier.NameAr,
                    DisplayName = ResolveDisplayName(po.Supplier.Name, po.Supplier.NameAr, isArabic)
                },
                po.Status,
                po.TotalAmount,
                po.ExpectedDate,
                po.ReceivedDate,
                po.CreatedAt,
                po.ItemCount,
                po.UnitCost,
                InvoiceId = po.SupplierInvoice?.Id,
                InvoiceNumber = po.SupplierInvoice?.InvoiceNumber ?? po.InvoiceNumber,
                InvoiceDate = po.SupplierInvoice?.InvoiceDate,
                InvoiceTotalAmount = po.SupplierInvoice?.TotalAmount ?? po.TotalAmount,
                TaxAmount = po.SupplierInvoice?.TaxAmount ?? 0,
                DiscountAmount = po.SupplierInvoice?.DiscountAmount ?? 0,
                PaidAmount = po.SupplierInvoice?.PaidAmount ?? 0,
                Balance = po.SupplierInvoice != null
                    ? po.SupplierInvoice.TotalAmount - po.SupplierInvoice.PaidAmount
                    : po.TotalAmount,
                PaymentStatus = po.SupplierInvoice?.Status.ToString() ?? "Unpaid",
                Items = po.Items.Select(i => new
                {
                    i.Id,
                    i.RawMaterialId,
                    RawMaterialName = ResolveRawMaterialName(i, isArabic),
                    i.Quantity,
                    i.UnitPrice
                }).ToList()
            }).ToList();

            return Ok(response);
        }

        [HttpGet("orders/paginated")]
        public async Task<IActionResult> GetPurchaseOrdersPaginated(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string searchTerm = "",
            [FromQuery] int? status = null,
            [FromQuery] string sortBy = "createdAt",
            [FromQuery] string sortOrder = "desc",
            [FromQuery] ExpenseInvoiceStatus? paymentStatus = null,
            CancellationToken ct = default)
        {
            var isArabic = IsArabicRequested(Request);
            var branchId = await GetCurrentBranchIdAsync(ct);
            var query = _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.SupplierInvoice)
                .Include(po => po.Items)
                    .ThenInclude(i => i.RawMaterial)
                .Where(po => po.BranchId == branchId)
                .AsQueryable();
            var normalizedSearchTerm = (searchTerm ?? string.Empty).Trim();

            // Filter by status if provided
            if (status.HasValue)
            {
                query = query.Where(po => (int) po.Status == status.Value);
            }

            if (paymentStatus == ExpenseInvoiceStatus.Unpaid)
                query = query.Where(po =>
                    po.SupplierInvoice == null
                    || po.SupplierInvoice.Status == ExpenseInvoiceStatus.Unpaid);
            else if (paymentStatus.HasValue)
                query = query.Where(po =>
                    po.SupplierInvoice != null
                    && po.SupplierInvoice.Status == paymentStatus.Value);

            // Search filter
            if (!string.IsNullOrWhiteSpace(normalizedSearchTerm))
            {
                query = query.Where(po =>
                    EF.Functions.ILike(po.OrderNumber, $"%{normalizedSearchTerm}%") ||
                    (po.SupplierInvoice != null
                        && EF.Functions.ILike(po.SupplierInvoice.InvoiceNumber, $"%{normalizedSearchTerm}%")) ||
                    (po.Supplier != null && EF.Functions.ILike(po.Supplier.Name, $"%{normalizedSearchTerm}%")));
            }

            // Sorting
            query = sortBy.ToLower() switch
            {
                "supplier" => sortOrder == "desc" ? query.OrderByDescending(po => po.Supplier.Name) : query.OrderBy(po => po.Supplier.Name),
                "amount" => sortOrder == "desc" ? query.OrderByDescending(po => po.TotalAmount) : query.OrderBy(po => po.TotalAmount),
                "date" => sortOrder == "desc" ? query.OrderByDescending(po => po.ExpectedDate) : query.OrderBy(po => po.ExpectedDate),
                _ => sortOrder == "desc" ? query.OrderByDescending(po => po.CreatedAt) : query.OrderBy(po => po.CreatedAt)
            };

            var totalCount = await query.CountAsync(ct);
            var orders = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var response = orders.Select(po => new
            {
                po.Id,
                po.OrderNumber,
                po.SupplierId,
                Supplier = new
                {
                    po.Supplier.Id,
                    po.Supplier.Name,
                    po.Supplier.NameAr,
                    DisplayName = ResolveDisplayName(po.Supplier.Name, po.Supplier.NameAr, isArabic)
                },
                po.Status,
                po.TotalAmount,
                po.ExpectedDate,
                po.ReceivedDate,
                po.CreatedAt,
                po.ItemCount,
                po.UnitCost,
                InvoiceId = po.SupplierInvoice?.Id,
                InvoiceNumber = po.SupplierInvoice?.InvoiceNumber ?? po.InvoiceNumber,
                InvoiceDate = po.SupplierInvoice?.InvoiceDate,
                InvoiceTotalAmount = po.SupplierInvoice?.TotalAmount ?? po.TotalAmount,
                TaxAmount = po.SupplierInvoice?.TaxAmount ?? 0,
                DiscountAmount = po.SupplierInvoice?.DiscountAmount ?? 0,
                PaidAmount = po.SupplierInvoice?.PaidAmount ?? 0,
                Balance = po.SupplierInvoice != null
                    ? po.SupplierInvoice.TotalAmount - po.SupplierInvoice.PaidAmount
                    : po.TotalAmount,
                PaymentStatus = po.SupplierInvoice?.Status.ToString() ?? "Unpaid",
                Items = po.Items.Select(i => new
                {
                    i.Id,
                    i.RawMaterialId,
                    RawMaterialName = ResolveRawMaterialName(i, isArabic),
                    i.Quantity,
                    i.UnitPrice
                }).ToList()
            }).ToList();

            return Ok(new PaginatedResponse<object>
            {
                Items = response.Cast<object>().ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        [HttpPost("orders")]
        public async Task<IActionResult> CreatePurchaseOrder(PurchaseOrder order, CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Initialize items if null
                if (order.Items == null || order.Items.Count == 0)
                {
                    return BadRequest("Purchase order must contain at least one item.");
                }

                // Validate all items: Quantity > 0 and UnitPrice > 0
                foreach (var item in order.Items)
                {
                    var validationError = ValidatePurchaseOrderItem(item);
                    if (validationError != null)
                        return BadRequest(validationError);
                }

                var branchId = await GetCurrentBranchIdAsync(ct);
                var itemMaterialIds = order.Items.Select(i => i.RawMaterialId).Distinct().ToList();
                var materials = await _context.RawMaterials
                    .AsNoTracking()
                    .Where(rm => itemMaterialIds.Contains(rm.Id))
                    .Select(rm => new { rm.Id, rm.Name, rm.NameAr })
                    .ToDictionaryAsync(x => x.Id, ct);

                if (materials.Count != itemMaterialIds.Count)
                    return BadRequest("One or more selected materials do not exist.");

                order.Id = Guid.NewGuid();
                order.TenantId = _tenantResolver.GetTenantId();
                order.BranchId = branchId;
                order.CreatedAt = DateTime.UtcNow;
                order.Status = PurchaseOrderStatus.Draft;

                // If a Supplier object is included in the request, detach it to prevent duplicate key errors
                if (order.Supplier != null && order.SupplierId != Guid.Empty)
                {
                    _context.Entry(order.Supplier).State = EntityState.Detached;
                    order.Supplier = null; // Clear the navigation property; only use SupplierId
                }

                foreach (var item in order.Items)
                {
                    item.Id = Guid.NewGuid();
                    item.TenantId = order.TenantId;
                    item.BranchId = branchId;
                    item.PurchaseOrderId = order.Id;
                    item.PurchaseOrder = null;
                    item.RawMaterial = null;
                    item.RawMaterialName = null;
                }
                
                SyncPurchaseOrderTotals(order);

                _context.PurchaseOrders.Add(order);
                await _context.SaveChangesAsync(ct);

                // Return clean response without circular references
                var supplier = await _context.Suppliers.FindAsync(new object[] { order.SupplierId }, ct);
                var isArabic = IsArabicRequested(Request);
                var response = new
                {
                    order.Id,
                    order.OrderNumber,
                    order.SupplierId,
                    Supplier = new
                    {
                        supplier?.Id,
                        supplier?.Name,
                        supplier?.NameAr,
                        DisplayName = supplier != null ? ResolveDisplayName(supplier.Name, supplier.NameAr, isArabic) : null
                    },
                    order.Status,
                    order.TotalAmount,
                    order.ExpectedDate,
                    order.CreatedAt,
                    order.ItemCount,
                    order.UnitCost,
                    Items = order.Items.Select(i => new
                    {
                        i.Id,
                        i.RawMaterialId,
                        RawMaterialName = ResolveDisplayName(materials[i.RawMaterialId].Name, materials[i.RawMaterialId].NameAr, isArabic),
                        i.Quantity,
                        i.UnitPrice
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        [HttpPut("orders/{id}")]
        public async Task<IActionResult> UpdatePurchaseOrder(Guid id, PurchaseOrder orderUpdate, CancellationToken ct)
        {
            try
            {
                var branchId = await GetCurrentBranchIdAsync(ct);
                var order = await _context.PurchaseOrders
                    .Include(po => po.Supplier)
                    .Include(po => po.Items)
                        .ThenInclude(i => i.RawMaterial)
                    .FirstOrDefaultAsync(po => po.Id == id && po.BranchId == branchId, ct);

                if (order == null)
                    return NotFound();

                // Only allow updates for Draft orders
                if (order.Status != PurchaseOrderStatus.Draft)
                    return BadRequest("Can only update draft orders");

                // Update supplier
                if (orderUpdate.SupplierId != Guid.Empty)
                {
                    order.SupplierId = orderUpdate.SupplierId;
                }

                // Update expected date
                if (orderUpdate.ExpectedDate != DateTime.MinValue)
                {
                    order.ExpectedDate = orderUpdate.ExpectedDate;
                }

                // Update items if provided
                if (orderUpdate.Items != null && orderUpdate.Items.Count > 0)
                {
                    foreach (var updateItem in orderUpdate.Items)
                    {
                        var validationError = ValidatePurchaseOrderItem(updateItem);
                        if (validationError != null)
                            return BadRequest(validationError);

                        var existingItem = order.Items.FirstOrDefault(i => i.RawMaterialId == updateItem.RawMaterialId);
                        if (existingItem == null)
                            return BadRequest("Item does not belong to this purchase order.");

                        existingItem.Quantity = updateItem.Quantity;
                        existingItem.UnitPrice = updateItem.UnitPrice;
                        existingItem.RawMaterialName = null;
                    }
                }

                SyncPurchaseOrderTotals(order);

                await _context.SaveChangesAsync(ct);

                // Reload supplier for response
                var supplier = await _context.Suppliers.FindAsync(new object[] { order.SupplierId }, ct);
                var isArabic = IsArabicRequested(Request);

                // Return clean response
                var response = new
                {
                    order.Id,
                    order.OrderNumber,
                    order.SupplierId,
                    Supplier = new
                    {
                        supplier?.Id,
                        supplier?.Name,
                        supplier?.NameAr,
                        DisplayName = supplier != null ? ResolveDisplayName(supplier.Name, supplier.NameAr, isArabic) : null
                    },
                    order.Status,
                    order.TotalAmount,
                    order.ExpectedDate,
                    order.CreatedAt,
                    order.ItemCount,
                    order.UnitCost,
                Items = order.Items.Select(i => new
                {
                    i.Id,
                    i.RawMaterialId,
                    RawMaterialName = ResolveRawMaterialName(i, isArabic),
                    i.Quantity,
                    i.UnitPrice
                }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("orders/{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] PurchaseOrderStatus status, CancellationToken ct)
        {
            var order = await _workflowService.UpdateStatusAsync(id, status, ct);
            var isArabic = IsArabicRequested(Request);
            var response = new
            {
                order.Id,
                order.OrderNumber,
                order.SupplierId,
                Supplier = new
                {
                    order.Supplier?.Id,
                    order.Supplier?.Name,
                    order.Supplier?.NameAr,
                    DisplayName = order.Supplier != null ? ResolveDisplayName(order.Supplier.Name, order.Supplier.NameAr, isArabic) : null
                },
                order.Status,
                order.TotalAmount,
                order.ExpectedDate,
                order.ReceivedDate,
                order.CreatedAt,
                order.ItemCount,
                order.UnitCost,
                Items = order.Items?.Select(i => new
                {
                    i.Id,
                    i.RawMaterialId,
                    RawMaterialName = ResolveRawMaterialName(i, isArabic),
                    i.Quantity,
                    i.UnitPrice
                }).ToList()
            };

            return Ok(response);
        }

        [HttpPost("purchases")]
        [Authorize(Roles = AppRoleGroups.AdminStrict)]
        public async Task<ActionResult<SimplePurchaseDetailsDto>> CreateSimplePurchase(
            [FromBody] SimplePurchaseCreateDto dto,
            CancellationToken ct)
        {
            var created = await _simplePurchaseService.CreateAsync(
                dto,
                GetCurrentUserId(),
                GetCurrentUserName(),
                IsArabicRequested(Request),
                ct);
            return CreatedAtAction(
                nameof(GetSimplePurchase),
                new { id = created.Id },
                created);
        }

        [HttpGet("purchases/{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminStrict)]
        public async Task<ActionResult<SimplePurchaseDetailsDto>> GetSimplePurchase(
            Guid id,
            CancellationToken ct)
        {
            var purchase = await _simplePurchaseService.GetDetailsAsync(
                id,
                IsArabicRequested(Request),
                ct);
            return Ok(purchase);
        }

        [HttpPost("purchases/{id:guid}/receive")]
        [Authorize(Roles = AppRoleGroups.AdminStrict)]
        public async Task<ActionResult<SimplePurchaseDetailsDto>> ReceiveSimplePurchase(
            Guid id,
            CancellationToken ct)
        {
            var purchase = await _simplePurchaseService.ReceiveAsync(
                id,
                IsArabicRequested(Request),
                ct);
            return Ok(purchase);
        }

        [HttpPost("purchases/{id:guid}/approve")]
        [Authorize(Roles = AppRoleGroups.AdminStrict)]
        public async Task<ActionResult<SimplePurchaseDetailsDto>> ApproveSimplePurchase(
            Guid id,
            CancellationToken ct)
        {
            var purchase = await _simplePurchaseService.ApproveAsync(
                id,
                IsArabicRequested(Request),
                ct);
            return Ok(purchase);
        }

        // --- Stock Batch Management ---

        [HttpGet("orders/{id}/batches")]
        public async Task<IActionResult> GetStockBatches(Guid id, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var orderStatus = await _context.PurchaseOrders
                .AsNoTracking()
                .Where(po => po.Id == id && po.BranchId == branchId)
                .Select(po => (PurchaseOrderStatus?)po.Status)
                .FirstOrDefaultAsync(ct);

            if (!orderStatus.HasValue)
            {
                return NotFound();
            }

            if (orderStatus == PurchaseOrderStatus.Cancelled)
            {
                return Ok(Array.Empty<StockBatchDetailDto>());
            }

            var batches = await _context.StockBatches
                .Include(sb => sb.RawMaterial)
                .AsNoTracking()
                .Where(sb => sb.PurchaseOrderId == id && sb.BranchId == branchId)
                .ToListAsync(ct);

            if (batches == null || batches.Count == 0)
            {
                return NotFound(new { message = $"No batches found for PO {id}" });
            }

            var isArabic = IsArabicRequested(Request);

            var response = batches.Select(b => new StockBatchDetailDto
            {
                Id = b.Id,
                MaterialId = b.MaterialId,
                MaterialName = b.RawMaterial?.Name ?? string.Empty,
                MaterialNameAr = b.RawMaterial?.NameAr,
                BatchNumber = b.BatchNumber,
                PurchaseOrderId = b.PurchaseOrderId,
                PurchaseOrderNumber = b.PurchaseOrderNumber,
                Quantity = b.Quantity,
                RemainingQuantity = b.RemainingQuantity,
                UnitCost = b.UnitCost,
                TotalCost = b.TotalCost,
                ExpiryDate = b.ExpiryDate,
                CreatedAt = b.CreatedAt,
                IsApproved = b.IsApproved
            }).ToList();

            return Ok(response);
        }

        private static void SyncPurchaseOrderTotals(PurchaseOrder order)
        {
            order.ItemCount = (int)(order.Items?.Sum(i => i.Quantity) ?? 0);
            order.UnitCost = (order.Items?.Count ?? 0) > 0
                ? order.Items.FirstOrDefault()?.UnitPrice ?? 0
                : 0;
            order.TotalAmount = order.Items?.Sum(i => i.Quantity * i.UnitPrice) ?? 0;
        }

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
            => (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

        private static string? ValidatePurchaseOrderItem(PurchaseOrderItem item)
        {
            if (item.Quantity <= 0)
                return "Item quantity must be greater than 0.";

            if (item.UnitPrice <= 0)
                return "Item unit price must be greater than 0.";

            return HasAllowedPurchaseUnitPriceScale(item.UnitPrice) ? null : UnitPricePrecisionError;
        }

        private static bool HasAllowedPurchaseUnitPriceScale(decimal value)
        {
            var scale = (decimal.GetBits(value)[3] >> 16) & 0x7F;
            return scale <= PurchaseUnitPriceDecimalPlaces;
        }

        private static bool IsArabicRequested(HttpRequest request)
        {
            var language = request.Headers["Accept-Language"].ToString();
            return language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDisplayName(string name, string? nameAr, bool isArabic)
        {
            return isArabic && !string.IsNullOrWhiteSpace(nameAr) ? nameAr : name;
        }

        private static string ResolveRawMaterialName(PurchaseOrderItem item, bool isArabic)
        {
            if (item.RawMaterial != null)
                return ResolveDisplayName(item.RawMaterial.Name, item.RawMaterial.NameAr, isArabic);

            return item.RawMaterialName ?? string.Empty;
        }

        private Guid? GetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("UserId")
                ?? User.FindFirstValue("userId");
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        private string? GetCurrentUserName()
            => User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("name")
                ?? User.Identity?.Name;
    }
}
