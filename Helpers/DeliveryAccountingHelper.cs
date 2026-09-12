using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers
{
    public sealed record DeliveryAccountingSnapshot(
        decimal FoodSubtotal,
        decimal CustomerDeliveryFee,
        decimal ActualDeliveryCost,
        decimal DeliveryMargin,
        decimal MarketplaceDeliveryFee,
        decimal MarketplaceServiceFee,
        decimal NetRestaurantRevenue);

    public static class DeliveryAccountingHelper
    {
        public static DeliveryAccountingSnapshot Calculate(Order order)
        {
            var foodSubtotal = Round(Math.Max(0m, order.Subtotal - order.DiscountAmount - order.DiscountGroupAmount));
            var restaurantServiceAndTax = Round(order.ServiceChargeAmount + order.TaxAmount);
            var customerDeliveryFee = 0m;
            var actualDeliveryCost = 0m;
            var marketplaceDeliveryFee = 0m;
            var marketplaceServiceFee = 0m;

            if (order.OrderType == OrderType.Delivery)
            {
                customerDeliveryFee = Round(order.DeliveryFee ?? 0m);
                actualDeliveryCost = Round(order.DeliveryCost ?? 0m);
            }

            if (order.OrderSource == OrderSource.Talabat)
            {
                marketplaceDeliveryFee = Round(order.TalabatDeliveryFee ?? 0m);
                marketplaceServiceFee = Round(order.TalabatServiceFee ?? 0m);
            }

            if (order.OrderSource == OrderSource.DeliveryPartner)
            {
                customerDeliveryFee = 0m;
                actualDeliveryCost = Round(order.ActualDeliveryCost);
                marketplaceDeliveryFee = 0m;
                marketplaceServiceFee = 0m;
            }

            var deliveryMargin = Round(customerDeliveryFee - actualDeliveryCost);
            var netRestaurantRevenue = Round(foodSubtotal + restaurantServiceAndTax + deliveryMargin);

            return new DeliveryAccountingSnapshot(
                foodSubtotal,
                customerDeliveryFee,
                actualDeliveryCost,
                deliveryMargin,
                marketplaceDeliveryFee,
                marketplaceServiceFee,
                netRestaurantRevenue);
        }

        public static void Apply(Order order)
        {
            var snapshot = Calculate(order);
            order.FoodSubtotal = snapshot.FoodSubtotal;
            order.CustomerDeliveryFee = snapshot.CustomerDeliveryFee;
            order.ActualDeliveryCost = snapshot.ActualDeliveryCost;
            order.DeliveryMargin = snapshot.DeliveryMargin;
            order.MarketplaceDeliveryFee = snapshot.MarketplaceDeliveryFee;
            order.MarketplaceServiceFee = snapshot.MarketplaceServiceFee;
            order.NetRestaurantRevenue = snapshot.NetRestaurantRevenue;
        }

        /// <summary>
        /// The revenue figure order profit is measured against. Delivery and marketplace orders
        /// are judged on what the restaurant actually keeps; everything else on the order total.
        /// Moved here from the FIFO engine so the retail ledger measures profit the same way
        /// instead of growing a second definition of it.
        /// </summary>
        public static decimal ProfitRevenue(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            if (order.NetRestaurantRevenue <= 0m)
                Apply(order);

            return order.NetRestaurantRevenue > 0m ? order.NetRestaurantRevenue : order.TotalAmount;
        }

        private static decimal Round(decimal amount)
            => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
