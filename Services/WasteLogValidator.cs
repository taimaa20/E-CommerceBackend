using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public static class WasteLogValidator
    {
        private static readonly HashSet<ManualWasteType> ManualCreateTypes = new()
        {
            ManualWasteType.GIFT,
            ManualWasteType.DAMAGED,
            ManualWasteType.STOLEN,
            ManualWasteType.STAFF_MEAL,
            ManualWasteType.WRONG_PREPARATION,
            ManualWasteType.CUSTOMER_COMPENSATION,
            ManualWasteType.OTHER
        };

        public static ManualWasteType ValidateCreate(WasteLogCreateDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var errors = new Dictionary<string, string[]>();

            if (!Enum.TryParse<ManualWasteType>(dto.WasteType, ignoreCase: true, out var wasteType) ||
                !ManualCreateTypes.Contains(wasteType))
            {
                errors[nameof(dto.WasteType)] = new[] { "Waste type is invalid." };
            }

            if (dto.ProductId.HasValue == dto.MaterialId.HasValue)
                errors[nameof(dto.ProductId)] = new[] { "Provide exactly one ProductId or MaterialId." };

            if (dto.Quantity <= 0)
                errors[nameof(dto.Quantity)] = new[] { "Quantity must be greater than zero." };

            if (string.IsNullOrWhiteSpace(dto.Reason))
                errors[nameof(dto.Reason)] = new[] { "Reason is required." };

            if (wasteType == ManualWasteType.STAFF_MEAL)
            {
                var hasEmployees = dto.EmployeeIds != null && dto.EmployeeIds.Any(id => id != Guid.Empty);
                if (!hasEmployees)
                    errors[nameof(dto.EmployeeIds)] = new[] { "Employee is required for staff meal waste." };
            }

            if (errors.Count > 0)
                throw new ValidationException(errors);

            return wasteType;
        }

        public static ManualWasteType ValidateUpdate(WasteLogUpdateDto dto)
            => ValidateCreate(dto);

        public static void ValidateMetadataUpdate(WasteLogUpdateDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(dto.Reason))
                errors[nameof(dto.Reason)] = new[] { "Reason is required." };

            if (dto.Quantity <= 0)
                errors[nameof(dto.Quantity)] = new[] { "Quantity must be greater than zero." };

            if (errors.Count > 0)
                throw new ValidationException(errors);
        }

        public static void ValidateSourceLinkedUpdate(WasteLogUpdateDto dto, WasteCategory category)
        {
            ValidateMetadataUpdate(dto);

            var errors = new Dictionary<string, string[]>();
            if (category == WasteCategory.CancelProduct && (!dto.ProductId.HasValue || dto.MaterialId.HasValue))
            {
                errors[nameof(dto.ProductId)] = new[] { "Provide exactly one ProductId." };
            }

            if (category == WasteCategory.ExpiryProduct && (!dto.MaterialId.HasValue || dto.ProductId.HasValue))
            {
                errors[nameof(dto.MaterialId)] = new[] { "Provide exactly one MaterialId." };
            }

            if (errors.Count > 0)
                throw new ValidationException(errors);
        }

        public static void EnsureEditable(WasteLog log)
        {
            if (log.DeletedAt.HasValue)
                throw new ConflictException("Deleted waste logs cannot be edited.");
        }

        public static void EnsurePending(WasteLog log)
        {
            if (log.Status != WasteLogStatus.Pending)
                throw new ConflictException("Only pending waste logs can be approved or rejected.");
        }
    }
}
