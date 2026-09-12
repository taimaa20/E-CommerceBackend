using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerDeliveryService : ICustomerDeliveryService
    {
        private const double EarthRadiusMeters = 6371000d;

        private readonly IDeliveryZoneService _deliveryZoneService;

        public CustomerDeliveryService(IDeliveryZoneService deliveryZoneService)
        {
            _deliveryZoneService = deliveryZoneService ?? throw new ArgumentNullException(nameof(deliveryZoneService));
        }

        public async Task<CustomerDeliveryCheckResponse> CheckZoneAsync(
            CustomerDeliveryCheckRequest request,
            CancellationToken ct)
        {
            var zones = await _deliveryZoneService.GetActiveForMainBranchAsync(ct);
            var match = zones
                .Where(IsConfiguredForCoordinates)
                .Select(z => new
                {
                    Zone = z,
                    Distance = CalculateDistanceMeters(
                        (double)request.Latitude,
                        (double)request.Longitude,
                        (double)z.CenterLatitude!.Value,
                        (double)z.CenterLongitude!.Value)
                })
                .Where(x => x.Distance <= x.Zone.RadiusMeters!.Value)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (match == null)
            {
                return new CustomerDeliveryCheckResponse
                {
                    IsAvailable = false,
                    DeliveryFee = 0m,
                    Message = zones.Any(IsConfiguredForCoordinates)
                        ? "Location is outside active delivery zones."
                        : "Coordinate-based delivery zones are not configured."
                };
            }

            return new CustomerDeliveryCheckResponse
            {
                IsAvailable = true,
                Zone = MapZone(match.Zone),
                DeliveryFee = match.Zone.DeliveryFee
            };
        }

        private static bool IsConfiguredForCoordinates(DeliveryZoneSelectDto zone)
            => zone.CenterLatitude.HasValue && zone.CenterLongitude.HasValue && zone.RadiusMeters.HasValue;

        private static CustomerDeliveryZoneDto MapZone(DeliveryZoneSelectDto zone)
            => new()
            {
                Id = zone.Id,
                NameEn = zone.NameEn,
                NameAr = zone.NameAr,
                Code = zone.Code,
                DeliveryFee = zone.DeliveryFee,
                PaymentMode = zone.PaymentMode.ToString()
            };

        private static double CalculateDistanceMeters(
            double latitude,
            double longitude,
            double zoneLatitude,
            double zoneLongitude)
        {
            var dLat = ToRadians(zoneLatitude - latitude);
            var dLon = ToRadians(zoneLongitude - longitude);
            var lat1 = ToRadians(latitude);
            var lat2 = ToRadians(zoneLatitude);

            var a = Math.Sin(dLat / 2d) * Math.Sin(dLat / 2d) +
                    Math.Cos(lat1) * Math.Cos(lat2) *
                    Math.Sin(dLon / 2d) * Math.Sin(dLon / 2d);

            return EarthRadiusMeters * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
    }
}
