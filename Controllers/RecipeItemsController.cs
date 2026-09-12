using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecipeItemsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly ITenantResolver _tenantResolver;

        public RecipeItemsController(
            IProductService productService,
            ITenantResolver tenantResolver)
        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        // POST: api/RecipeItems
        [HttpPost]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<RecipeItem>> CreateRecipeItem(
            RecipeItem recipeItem,
            CancellationToken ct)
        {
            if (recipeItem.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            var tenantId = _tenantResolver.GetTenantId();
            var created = await _productService.AddRecipeItemAsync(
                recipeItem.ProductId,
                new RecipeItemCreateDto
                {
                    RawMaterialId = recipeItem.RawMaterialId,
                    Amount = recipeItem.Amount
                },
                tenantId,
                ct);

            return Ok(created);
        }
        
        // PUT: api/RecipeItems/5
        [HttpPut("{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> UpdateRecipeItem(
            Guid id,
            [FromBody] UpdateRecipeItemDto dto,
            CancellationToken ct)
        {
            if (dto.Amount <= 0) return BadRequest("Amount must be greater than zero.");

            var tenantId = _tenantResolver.GetTenantId();
            await _productService.UpdateRecipeItemAsync(id, dto.Amount, tenantId, ct);

            return Ok();
        }

        // DELETE: api/RecipeItems/5
        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> DeleteRecipeItem(Guid id, CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            await _productService.DeleteRecipeItemAsync(id, tenantId, ct);
            return NoContent();
        }
    }

    public class UpdateRecipeItemDto
    {
        public decimal Amount { get; set; }
    }
}
