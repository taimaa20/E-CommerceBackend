using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Modules.CustomerMobile.DTOs;

public sealed class CustomerCheckoutCreateRequest
{
    [Required]
    public Guid OrderId { get; init; }

    [Required]
    [RegularExpression("^order$", ErrorMessage = "Action must be 'order'.")]
    public string Action { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^(en|ar)$", ErrorMessage = "Language must be 'en' or 'ar'.")]
    public string Lang { get; init; } = "en";
}
