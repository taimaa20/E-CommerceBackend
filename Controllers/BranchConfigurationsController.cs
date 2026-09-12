using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/branch-configurations")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public class BranchConfigurationsController : ControllerBase
    {
        private readonly IBranchConfigurationService _service;

        public BranchConfigurationsController(IBranchConfigurationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet("options")]
        public async Task<IActionResult> GetOptions(CancellationToken ct)
        {
            var options = await _service.GetOptionsAsync(ct);
            return Ok(options);
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts(
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? productId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchProductsAsync(branchId, productId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("products")]
        public async Task<IActionResult> CreateProduct([FromBody] BranchProductConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchProductAsync(dto, ct);
            return CreatedAtAction(nameof(GetProducts), new { id = created.Id }, created);
        }

        [HttpPut("products/{id:guid}")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] BranchProductConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchProductAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("products/{id:guid}")]
        public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchProductAsync(id, ct);
            return NoContent();
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories(
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? categoryId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchCategoriesAsync(branchId, categoryId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] BranchCategoryConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchCategoryAsync(dto, ct);
            return CreatedAtAction(nameof(GetCategories), new { id = created.Id }, created);
        }

        [HttpPut("categories/{id:guid}")]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] BranchCategoryConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchCategoryAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("categories/{id:guid}")]
        public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchCategoryAsync(id, ct);
            return NoContent();
        }

        [HttpGet("subcategories")]
        public async Task<IActionResult> GetSubcategories(
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? subcategoryId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchSubcategoriesAsync(branchId, subcategoryId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("subcategories")]
        public async Task<IActionResult> CreateSubcategory([FromBody] BranchSubcategoryConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchSubcategoryAsync(dto, ct);
            return CreatedAtAction(nameof(GetSubcategories), new { id = created.Id }, created);
        }

        [HttpPut("subcategories/{id:guid}")]
        public async Task<IActionResult> UpdateSubcategory(Guid id, [FromBody] BranchSubcategoryConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchSubcategoryAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("subcategories/{id:guid}")]
        public async Task<IActionResult> DeleteSubcategory(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchSubcategoryAsync(id, ct);
            return NoContent();
        }

        [HttpGet("modifiers")]
        public async Task<IActionResult> GetModifiers(
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? modifierId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchModifiersAsync(branchId, modifierId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("modifiers")]
        public async Task<IActionResult> CreateModifier([FromBody] BranchModifierConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchModifierAsync(dto, ct);
            return CreatedAtAction(nameof(GetModifiers), new { id = created.Id }, created);
        }

        [HttpPut("modifiers/{id:guid}")]
        public async Task<IActionResult> UpdateModifier(Guid id, [FromBody] BranchModifierConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchModifierAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("modifiers/{id:guid}")]
        public async Task<IActionResult> DeleteModifier(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchModifierAsync(id, ct);
            return NoContent();
        }

        [HttpGet("modifier-groups")]
        public async Task<IActionResult> GetModifierGroups(
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? modifierGroupId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchModifierGroupsAsync(branchId, modifierGroupId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("modifier-groups")]
        public async Task<IActionResult> CreateModifierGroup([FromBody] BranchModifierGroupConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchModifierGroupAsync(dto, ct);
            return CreatedAtAction(nameof(GetModifierGroups), new { id = created.Id }, created);
        }

        [HttpPut("modifier-groups/{id:guid}")]
        public async Task<IActionResult> UpdateModifierGroup(Guid id, [FromBody] BranchModifierGroupConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchModifierGroupAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("modifier-groups/{id:guid}")]
        public async Task<IActionResult> DeleteModifierGroup(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchModifierGroupAsync(id, ct);
            return NoContent();
        }

        [HttpGet("product-options")]
        public async Task<IActionResult> GetProductOptions(
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? productOptionId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchProductOptionsAsync(branchId, productOptionId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("product-options")]
        public async Task<IActionResult> CreateProductOption([FromBody] BranchProductOptionConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchProductOptionAsync(dto, ct);
            return CreatedAtAction(nameof(GetProductOptions), new { id = created.Id }, created);
        }

        [HttpPut("product-options/{id:guid}")]
        public async Task<IActionResult> UpdateProductOption(Guid id, [FromBody] BranchProductOptionConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchProductOptionAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("product-options/{id:guid}")]
        public async Task<IActionResult> DeleteProductOption(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchProductOptionAsync(id, ct);
            return NoContent();
        }

        [HttpGet("payment-methods")]
        public async Task<IActionResult> GetPaymentMethods(
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? paymentMethodId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchPaymentMethodsAsync(branchId, paymentMethodId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("payment-methods")]
        public async Task<IActionResult> CreatePaymentMethod([FromBody] BranchPaymentMethodConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchPaymentMethodAsync(dto, ct);
            return CreatedAtAction(nameof(GetPaymentMethods), new { id = created.Id }, created);
        }

        [HttpPut("payment-methods/{id:guid}")]
        public async Task<IActionResult> UpdatePaymentMethod(Guid id, [FromBody] BranchPaymentMethodConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchPaymentMethodAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("payment-methods/{id:guid}")]
        public async Task<IActionResult> DeletePaymentMethod(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchPaymentMethodAsync(id, ct);
            return NoContent();
        }

        [HttpGet("delivery-partners")]
        public async Task<IActionResult> GetDeliveryPartners(
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? deliveryPartnerId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchDeliveryPartnersAsync(branchId, deliveryPartnerId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("delivery-partners")]
        public async Task<IActionResult> CreateDeliveryPartner([FromBody] BranchDeliveryPartnerConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchDeliveryPartnerAsync(dto, ct);
            return CreatedAtAction(nameof(GetDeliveryPartners), new { id = created.Id }, created);
        }

        [HttpPut("delivery-partners/{id:guid}")]
        public async Task<IActionResult> UpdateDeliveryPartner(Guid id, [FromBody] BranchDeliveryPartnerConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchDeliveryPartnerAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("delivery-partners/{id:guid}")]
        public async Task<IActionResult> DeleteDeliveryPartner(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchDeliveryPartnerAsync(id, ct);
            return NoContent();
        }

        [HttpGet("delivery-zones")]
        public async Task<IActionResult> GetDeliveryZones(
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? deliveryZoneId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchDeliveryZonesAsync(branchId, deliveryZoneId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("delivery-zones")]
        public async Task<IActionResult> CreateDeliveryZone([FromBody] BranchDeliveryZoneConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchDeliveryZoneAsync(dto, ct);
            return CreatedAtAction(nameof(GetDeliveryZones), new { id = created.Id }, created);
        }

        [HttpPut("delivery-zones/{id:guid}")]
        public async Task<IActionResult> UpdateDeliveryZone(Guid id, [FromBody] BranchDeliveryZoneConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchDeliveryZoneAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("delivery-zones/{id:guid}")]
        public async Task<IActionResult> DeleteDeliveryZone(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchDeliveryZoneAsync(id, ct);
            return NoContent();
        }

        [HttpGet("printers")]
        public async Task<IActionResult> GetPrinters(
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? printerId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchPrintersAsync(branchId, printerId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("printers")]
        public async Task<IActionResult> CreatePrinter([FromBody] BranchPrinterConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchPrinterAsync(dto, ct);
            return CreatedAtAction(nameof(GetPrinters), new { id = created.Id }, created);
        }

        [HttpPut("printers/{id:guid}")]
        public async Task<IActionResult> UpdatePrinter(Guid id, [FromBody] BranchPrinterConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchPrinterAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("printers/{id:guid}")]
        public async Task<IActionResult> DeletePrinter(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchPrinterAsync(id, ct);
            return NoContent();
        }

        [HttpGet("offers")]
        public async Task<IActionResult> GetOffers(
            [FromQuery] Guid? branchId,
            [FromQuery] int? offerId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchOffersAsync(branchId, offerId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("offers")]
        public async Task<IActionResult> CreateOffer([FromBody] BranchOfferConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchOfferAsync(dto, ct);
            return CreatedAtAction(nameof(GetOffers), new { id = created.Id }, created);
        }

        [HttpPut("offers/{id:guid}")]
        public async Task<IActionResult> UpdateOffer(Guid id, [FromBody] BranchOfferConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchOfferAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("offers/{id:guid}")]
        public async Task<IActionResult> DeleteOffer(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchOfferAsync(id, ct);
            return NoContent();
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings(
            [FromQuery] Guid? branchId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetBranchSettingsAsync(branchId, search, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("settings")]
        public async Task<IActionResult> CreateSettings([FromBody] BranchSettingsConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateBranchSettingsAsync(dto, ct);
            return CreatedAtAction(nameof(GetSettings), new { id = created.Id }, created);
        }

        [HttpPut("settings/{id:guid}")]
        public async Task<IActionResult> UpdateSettings(Guid id, [FromBody] BranchSettingsConfigurationUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateBranchSettingsAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("settings/{id:guid}")]
        public async Task<IActionResult> DeleteSettings(Guid id, CancellationToken ct)
        {
            await _service.DeleteBranchSettingsAsync(id, ct);
            return NoContent();
        }
    }
}
