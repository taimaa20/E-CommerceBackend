using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Printing
{
    public interface IKitchenTicketBuilder
    {
        byte[] BuildKitchenTicket(Order order, Kitchen kitchen, IReadOnlyList<OrderItem> items, int codePage, bool isReprint = false);

        string BuildKitchenTicketHtml(Order order, Kitchen kitchen, IReadOnlyList<OrderItem> items, bool isReprint = false);

        /// <summary>
        /// Build a customer receipt that mirrors the frontend HTML design as
        /// closely as ESC/POS allows: business header, address, phone, tax
        /// number, order header, items + modifiers, full totals breakdown,
        /// footer note, and a QR code to the menu.
        /// </summary>
        byte[] BuildCustomerReceipt(Order order, IReadOnlyList<OrderItem> items, int codePage, SystemSettings? settings);
    }
}
