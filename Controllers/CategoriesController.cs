using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories.Category;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Services.Categroy;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
 
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryRepository _ICategoryRepository;
        private readonly ISubcategoryService _subcategoryService;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchConfigurationService _branchConfigurationService;

        public CategoriesController(
            ICategoryRepository ICategoryRepository,
            ISubcategoryService subcategoryService,
            ITenantResolver tenantResolver,
            IBranchConfigurationService branchConfigurationService)
        {
             _ICategoryRepository = ICategoryRepository;
             _subcategoryService = subcategoryService ?? throw new ArgumentNullException(nameof(subcategoryService));
             _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
             _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
        }
        // GET: api/categories
        [HttpGet]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]

        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories()
        {
            var isArabic = GeneralHelper.IsArabicRequested(Request);

            var categories = await _ICategoryRepository.GetCategories(isArabic);
            if (categories == null) return BadRequest();
            return Ok(categories);
        }

        // GET: api/categories/paginated?pageNumber=1&pageSize=10&search=...&isActive=...
        [HttpGet("paginated")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<ActionResult<PaginatedResponse<CategoryDto>>> GetCategoriesPaginated(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null)
        {
            var isArabic = GeneralHelper.IsArabicRequested(Request);
            var items =await _ICategoryRepository.GetCategoriesPaginated(pageNumber,pageSize,search,isActive,isArabic);
            if (items!=null&& items.Count>0)
            return Ok(new PaginatedResponse<CategoryDto>
            {
                Items = items,
                TotalCount = items.Count,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
            return BadRequest();
        }

        // GET: api/categories/{id}
        // get category by id 
        [HttpGet("{id}")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]

        public async Task<ActionResult<CategoryDto>> GetCategory(Guid id)
        {
            var isArabic = GeneralHelper.IsArabicRequested(Request);
            var category =await _ICategoryRepository.GetCategoryById(id,isArabic);

            if (category == null)
                return NotFound();

            return Ok(category);
        }

        // POST: api/categories
        [HttpPost]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<CategoryDto>> CreateCategory(CategoryCreateDto dto, CancellationToken ct)
        {
            // Validate: name must not be whitespace
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Category name (EN) is required." });

            // Check duplicate name (case-insensitive, per tenant via global filter)
            var duplicateEn = await _ICategoryRepository.IsDuplicatedEn(dto.Name);
            if (duplicateEn)
                return Conflict(new { message = $"A category with name '{dto.Name.Trim()}' already exists." });

            if (!string.IsNullOrWhiteSpace(dto.NameAr))
            {
                var nameAr = dto.NameAr.Trim();
                var duplicateAr = await _ICategoryRepository.IsDuplicatedEn(dto.NameAr);
                if (duplicateAr)
                    return Conflict(new { message = $"A category with Arabic name '{nameAr}' already exists." });
            }
            var tenantId = _tenantResolver.GetTenantId();
            var category =  await _ICategoryRepository.CreateCategory(dto);
            await _branchConfigurationService.EnsureMainBranchCategoriesAsync(
                new[] { new MainBranchConfigurationSeed(tenantId, category.Id, category.SortOrder) },
                ct);

            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                NameAr = category.NameAr,
                DisplayName = category.Name,
                Description = category.Description,
                ImageUrl = string.IsNullOrWhiteSpace(category.ImageUrl) ? null : category.ImageUrl.Trim(),
                SortOrder = category.SortOrder,
                DisplayMode = (int)category.DisplayMode,
                IsActive = category.IsActive
            });
        }

        // PUT: api/categories/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> UpdateCategory(Guid id, CategoryUpdateDto dto)
        {
          
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Category name (EN) is required." });

            // Check duplicate name (case-insensitive, per tenant via global filter)
            //var duplicateEn = await _ICategoryRepository.IsDuplicatedEn(dto.Name);
            //if (duplicateEn)
            //    return Conflict(new { message = $"A category with name '{dto.Name.Trim()}' already exists." });

            //if (!string.IsNullOrWhiteSpace(dto.NameAr))
            //{
            //    var nameAr = dto.NameAr.Trim();
            //    var duplicateAr = await _ICategoryRepository.IsDuplicatedEn(dto.NameAr);
            //    if (duplicateAr)
            //        return Conflict(new { message = $"A category with Arabic name '{nameAr}' already exists." });
            //}
            var updatedCategory = await _ICategoryRepository.UpdateCategory(id,dto);
            if (updatedCategory)
                return Ok();
            return NotFound();
        }

        // DELETE: api/categories/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
        {
         
            // Check if any products are using this category
            var hasProducts = await  _ICategoryRepository.HasProductRelated(id);
            if (hasProducts)
            {
                return BadRequest(new { message = "This category is being used by products. First, move the products to another category." });
            }

            await _branchConfigurationService.RemoveCategoryBranchConfigurationsAsync(id, ct);
            var DeleteCategory = await  _ICategoryRepository.DeleteCategory(id);
           if (!DeleteCategory) return NotFound();

            return Ok();
        }

        // GET: api/categories/public (for QR menu)
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetPublicCategories()
        {
            var isArabic = GeneralHelper.IsArabicRequested(Request);
            var categories=await _ICategoryRepository.GetPublicCategories(isArabic);
            if (categories == null) return NotFound();
            return Ok(categories);
        }

        [HttpGet("{categoryId}/subcategories")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<ActionResult<IEnumerable<SubcategoryDto>>> GetSubcategories(Guid categoryId, CancellationToken ct)
        {
            try
            {
                var isArabic = GeneralHelper.IsArabicRequested(Request);
                return Ok(await _subcategoryService.GetByCategoryAsync(categoryId, isArabic, ct));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("{categoryId}/subcategories")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<SubcategoryDto>> CreateSubcategory(Guid categoryId, [FromBody] SubcategoryCreateDto dto, CancellationToken ct)
        {
            try
            {
                var isArabic = GeneralHelper.IsArabicRequested(Request);
                var subcategory = await _subcategoryService.CreateAsync(categoryId, dto, isArabic, ct);
                return CreatedAtAction(nameof(GetSubcategories), new { categoryId }, subcategory);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("subcategories/{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<SubcategoryDto>> UpdateSubcategory(Guid id, [FromBody] SubcategoryUpdateDto dto, CancellationToken ct)
        {
            try
            {
                var isArabic = GeneralHelper.IsArabicRequested(Request);
                return Ok(await _subcategoryService.UpdateAsync(id, dto, isArabic, ct));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPatch("subcategories/{id}/active")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<SubcategoryDto>> SetSubcategoryActive(Guid id, [FromBody] SubcategoryActiveDto dto, CancellationToken ct)
        {
            try
            {
                var isArabic = GeneralHelper.IsArabicRequested(Request);
                return Ok(await _subcategoryService.SetActiveAsync(id, dto.IsActive, isArabic, ct));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpDelete("subcategories/{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> DeleteSubcategory(Guid id, CancellationToken ct)
        {
            try
            {
                await _subcategoryService.DeleteAsync(id, ct);
                return Ok();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{categoryId}/subcategories/reorder")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<IEnumerable<SubcategoryDto>>> ReorderSubcategories(Guid categoryId, [FromBody] List<SubcategoryReorderDto> items, CancellationToken ct)
        {
            try
            {
                var isArabic = GeneralHelper.IsArabicRequested(Request);
                return Ok(await _subcategoryService.ReorderAsync(categoryId, items, isArabic, ct));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

      
    }





}
