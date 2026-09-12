namespace RestaurantPos.Api.Services.Printing
{
    public interface IPrintDispatcher
    {
        /// <summary>
        /// Dispatches a freshly-created order: customer receipt + per-kitchen
        /// tickets covering ALL items on the order.
        /// </summary>
        /// <param name="cashierUserId">
        /// Optional caller (cashier or waiter acting as cashier) — used to
        /// resolve their assigned receipt printer. Null falls back to the
        /// tenant default printer, then to any active receipt printer.
        /// </param>
        Task DispatchOrderCreatedAsync(Guid orderId, Guid tenantId, Guid? cashierUserId, CancellationToken ct);

        /// <summary>
        /// Dispatches per-kitchen tickets for ONLY the newly added items on an
        /// existing order. Receipt is not reprinted.
        /// </summary>
        Task DispatchItemsAddedAsync(Guid orderId, Guid tenantId, IReadOnlyCollection<Guid> addedItemIds, CancellationToken ct);

        /// <summary>
        /// Re-enqueues a previously-completed or dead-letter job. Used by the
        /// manual reprint endpoint.
        /// </summary>
        Task<Guid> ReprintAsync(Guid jobId, Guid tenantId, CancellationToken ct);

        Task<DTOs.ResendKitchenTicketResponse> ResendKitchenTicketsAsync(
            Guid orderId,
            Guid tenantId,
            IReadOnlyCollection<Guid> kitchenIds,
            CancellationToken ct);

        /// <summary>
        /// Enqueue a customer receipt where the payload is a frontend-rendered
        /// PNG (base64). The backend converts it to ESC/POS raster bytes and
        /// queues it like any other receipt job. Returns true if a receipt
        /// printer is configured and the job was enqueued; false otherwise.
        /// </summary>
        Task<bool> DispatchReceiptFromImageAsync(
            Guid orderId, Guid tenantId, Guid? cashierUserId, byte[] imageBytes, CancellationToken ct);

        Task<bool> DispatchReceiptFromHtmlAsync(
            Guid orderId, Guid tenantId, Guid? cashierUserId, string htmlContent, CancellationToken ct);

        /// <summary>
        /// Returns the cashier's resolved receipt printer config so the frontend
        /// can cache it locally and route prints directly to the in-store agent
        /// when the cloud is unreachable. Same three-tier resolution as the
        /// online dispatch path.
        /// </summary>
        Task<DTOs.MyReceiptPrinterDto?> GetMyReceiptPrinterConfigAsync(
            Guid? cashierUserId, CancellationToken ct);

        /// <summary>
        /// Enqueue a customer receipt for an existing order on demand. Used by
        /// "Print receipt" buttons in the POS UI to avoid the browser print
        /// popup — the bytes go straight to the configured network receipt
        /// printer.
        /// Returns true if a receipt printer is configured and a job was
        /// enqueued; false if there is no active receipt printer (caller may
        /// fall back to a browser print).
        /// </summary>
        Task<bool> DispatchReceiptOnDemandAsync(Guid orderId, Guid tenantId, Guid? cashierUserId, CancellationToken ct);
    }
}
