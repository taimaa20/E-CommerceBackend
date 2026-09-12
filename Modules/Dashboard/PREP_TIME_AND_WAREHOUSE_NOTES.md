# Prep-Time (ReadyAt) & Warehouse-Aware Inventory — Implementation Notes

## 1. Canonical Preparation Time — `Order.ReadyAt`

**What:** `Order.ReadyAt` (nullable `timestamp with time zone`, migration `AddOrderReadyAt`) is the
canonical kitchen-prep-complete timestamp for the whole system.

**Where it is populated:** `Helpers/OrderCompletion.cs` →
`StampReadyAtIfNeeded(order)`, called from `ApplyStatus(order)` (the single status-derivation
helper) and from `OrdersController.RecalculateOrderAfterItemCancellation` (the one path that sets
`Status = Ready` without going through `ApplyStatus`).

**When it is populated:** the **first** moment **every** item on the order is ready
(`OrderItems.Count > 0 && OrderItems.All(IsReady)`). It is **never overwritten** once set, and is
**never** stamped for an order with no items.

**Which order statuses participate:** any order whose items all become ready — reached via the
kitchen "mark item ready" (`PUT /orders/{id}/items/{itemId}/ready`) or "mark order ready"
(`PUT /orders/{id}/ready`) endpoints, both of which call `ApplyStatus`. This covers every order
type with a kitchen preparation workflow (Dine-in, Takeaway, Delivery).

**How the KPI is calculated:** `OperationalMetricsService.BuildHealthAsync` →
`AveragePrepMinutes = avg(ReadyAt − CreatedAt)` over window-scoped orders **with a non-null
`ReadyAt`**. `PrepOrderCount` reports how many orders qualified.

**Excluded orders (intentional):**
- Legacy orders created before this field existed (`ReadyAt == null`).
- Orders that never reached full readiness (cancelled before ready, abandoned, etc.).
- These are excluded rather than estimated. When `PrepOrderCount == 0` the dashboard KPI and the
  Excel export show an empty state ("—"), never a misleading `0`.

**Reuse:** `ReadyAt` is a plain order field — future branch/warehouse reports and any other
dashboard can aggregate it without a further schema change.

---

## 2. Warehouse-Aware Inventory

Per-warehouse stock lives in `RawMaterialInventory` (`WarehouseId`, `Quantity`,
`MinimumQuantity`, `ReorderLevel`). The filter pipeline carries `DashboardFilterDto.WarehouseId`,
applied **once** in the shared `MetricQueryBuilder.RawMaterialInventories()`.

### Stock-level definitions — `Helpers/StockLevelPolicy.cs` (single source of truth)
Reused by dashboards, the warehouse dashboard, reports, notifications and replenishment.
States intentionally **overlap**:
- **Out of stock:** `Quantity <= 0`
- **Low stock:** `Quantity > 0 && Quantity <= MinimumQuantity`
- **Reorder required:** `Quantity <= ReorderLevel`

### Which KPIs are warehouse-aware vs global

| KPI | Warehouse-aware? | Source when a warehouse is selected |
|---|---|---|
| Inventory Value | ✅ Yes | `Σ RawMaterialInventory.Quantity × RawMaterial.CostPerUnit` |
| Out of Stock | ✅ Yes | `RawMaterialInventory.Quantity` vs policy |
| Low Stock | ✅ Yes | `Quantity` vs `MinimumQuantity` |
| Reorder Required | ✅ Yes | `Quantity` vs `ReorderLevel` |
| Expiring Soon (KPI + list) | ❌ Global | `StockBatch` — **no `WarehouseId`** |
| Waste (cost, %, by-type, by-employee, top-wasted, trend) | ❌ Global | `WasteLog` — **no `WarehouseId`** |
| Inventory Alerts (low/expiry) | ❌ Global | `RawMaterial` aggregate / `StockBatch` |

**All-warehouses mode (default):** uses the aggregate `RawMaterial` snapshot with
`MinimumAlertLevel` as both the low and reorder threshold — **byte-identical to the previous
behaviour** (fully backward compatible). When only one warehouse exists, the filter chip is hidden
and global == that warehouse.

The Inventory dashboard shows a clear banner when a specific warehouse is selected, stating that
stock figures reflect that warehouse while Waste & Expiring are across all warehouses. We do **not**
approximate Waste/Expiring by a material's `DefaultWarehouseId` — that would fabricate warehouse
data the schema does not have.

### Schema changes required to make the remaining KPIs warehouse-aware (future sprint)
1. **`WasteLog.WarehouseId`** (nullable → backfill → required): stamp the warehouse at waste-logging
   time so Waste KPIs can filter accurately.
2. **`StockBatch.WarehouseId`**: tag each batch's storage location so Expiring Soon / Inventory
   Alerts can filter and so FIFO valuation becomes per-warehouse.
3. Populate both through the existing inventory/waste workflows (not via material defaults).
4. Once present, extend `MetricQueryBuilder.WasteLogs()` / `StockBatches()` with the same
   `WarehouseId` predicate already used for `RawMaterialInventories()`, and the dashboard becomes
   fully warehouse-aware with no calculator rewrites.
