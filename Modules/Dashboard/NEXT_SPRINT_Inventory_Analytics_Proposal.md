# Next-Sprint Enhancement Proposal — Inventory Analytics

> **Status: PROPOSAL ONLY — no code in this sprint.** Prepared during the dashboard
> validation sprint. The current Inventory dashboard scope (Stock Health, Inventory
> Value, Low/Out/Reorder, Expiring, Waste, Alerts) is intentionally unchanged.

## Data-model reality check (grounds every estimate below)

| Entity | Today's role | Gap for analytics |
|---|---|---|
| `RawMaterial` | `CurrentStock`, `MinimumAlertLevel`, `CostPerUnit`, `Unit`, `DefaultWarehouseId` | Single live snapshot — **no historical stock/cost snapshots** |
| `StockBatch` | Per-batch `RemainingQuantity`, `UnitCost`, `ExpiryDate` | FIFO valuation source; batches lack consumed/received movement rows |
| `InventoryTransaction` | **Waste-only** (`WasteLogId` is non-nullable, `TransactionType` defaults to `Waste`) | **Not a general ledger** — cannot represent purchases/consumption/adjustments |
| `RecipeItem` | `ProductId → RawMaterialId`, `Amount` | Enables consumption *derivation* from sold `OrderItem`s |
| `PurchaseOrder` / `PurchaseOrderItem` | Procurement inbound | Inbound movements + supplier cost basis |
| `Supplier` | Supplier master | Supplier analytics join key |
| `WasteLog` | Waste outbound | Already surfaced on current dashboard |

**The single biggest architectural item:** there is **no append-only stock-movement ledger**.
Inbound (`PurchaseOrderItem`), consumption (`OrderItem × RecipeItem`), waste (`WasteLog`),
and adjustments (not captured today) live in separate shapes. Several proposals below
either (a) `UNION` these sources into a read-model, or (b) justify introducing a proper
`StockLedgerEntry` table. Decide (a) vs (b) early — it is the backbone of 4 of these features.

---

## P0 — Expired Inventory (carried over from this sprint)

- **Business objective:** Give managers a dedicated view of stock that has **already
  expired but is still on hand**, so it can be written off / actioned (removed from the
  "Expiring soon" list during this sprint to avoid misleading mixed data).
- **Data sources:** `StockBatch` where `RemainingQuantity > 0 && ExpiryDate < now`.
- **APIs:** extend `GET /api/dashboards/inventory` snapshot with `expiredInventory` (KPI + list).
- **Queries:** count + `Σ RemainingQuantity × UnitCost`; list top-N by at-risk value, oldest first.
- **KPIs:** Expired item count, Expired value, Oldest expiry age.
- **Widgets:** KPI card + ranked list (mirror the existing "Expiring soon" list).
- **Architectural impact:** **Low** — same pattern as existing expiring widgets; additive DTO field.
- **Dependencies:** none beyond `StockBatch`.

---

## Consumption Dashboard
- **Objective:** Show how much raw material was actually *consumed* by sales over a period.
- **Data sources:** `OrderItem` (Confirmed) × `RecipeItem.Amount` per product; `RawMaterial` for unit/cost.
- **APIs:** new `GET /api/dashboards/inventory/consumption` (own scope + filter context).
- **Queries:** `Σ (OrderItem.Quantity × RecipeItem.Amount)` grouped by `RawMaterialId`, valued at `CostPerUnit`.
- **KPIs:** Total consumed cost, Top consumed materials, Consumption vs sales ratio.
- **Widgets:** ranked materials, daily consumption trend, cost contribution donut.
- **Architectural impact:** **Medium** — recipe explosion query; reuse `OrderLifecyclePredicates.ConfirmedItem` + `MetricQueryBuilder` window. No schema change.
- **Dependencies:** Recipes must be complete/maintained; modifiers consumption (if recipe-bearing) must be decided.

## Raw Material Usage
- **Objective:** Per-material usage breakdown (consumption + waste) to spot over-use/shrinkage.
- **Data sources:** consumption (above) + `WasteLog`/`InventoryTransaction` (waste) per material.
- **APIs:** fold into the consumption endpoint as a per-material drill-down.
- **Queries:** join derived consumption with waste by `MaterialId`; compute usage = consumption + waste.
- **KPIs:** Usage per material, Waste-to-usage %, Theoretical vs actual variance.
- **Widgets:** per-material table with consumption/waste/variance columns.
- **Architectural impact:** **Medium** — depends on Consumption; reuses `WasteMetricProjection`.
- **Dependencies:** Consumption Dashboard.

## Stock Movement Analytics
- **Objective:** Unified in/out movement timeline per material (received, consumed, wasted, adjusted).
- **Data sources:** `PurchaseOrderItem` (in), derived consumption (out), `WasteLog` (out), adjustments (see below).
- **APIs:** new `GET /api/dashboards/inventory/movements`.
- **Queries:** `UNION` of typed movement rows OR query a new `StockLedgerEntry` read-model.
- **KPIs:** Net movement, Inbound vs outbound, Movement by type.
- **Widgets:** movement ledger table, stacked in/out trend.
- **Architectural impact:** **High** — needs the movement read-model decision (UNION view vs new `StockLedgerEntry` table + backfill). This is the keystone feature.
- **Dependencies:** Procurement module, Recipes, Waste; ideally a ledger.

## Inventory Adjustment Analytics
- **Objective:** Track manual stock corrections (who/when/why/value) for audit + shrinkage control.
- **Data sources:** **not captured today** as a first-class entity — manual changes currently surface only as `ManualMaterial`/`ManualProduct` waste.
- **APIs:** new endpoint once adjustments are first-class.
- **Queries:** group adjustments by reason/user/material.
- **KPIs:** Adjustment count, Net adjustment value, Adjustments by user/reason.
- **Widgets:** adjustment audit table, value-impact trend.
- **Architectural impact:** **High** — likely needs a new `StockAdjustment` entity + write path; cannot be derived reliably from existing data.
- **Dependencies:** ties into Stock Movement ledger; Audit module patterns.

## Product Performance
- **Objective:** Rank products by contribution (revenue, margin, units) for menu engineering.
- **Data sources:** `OrderItem` (Confirmed) + `Product`; cost from recipe explosion or `OrderItem.StockDeductedCost`.
- **APIs:** can extend Financial best/worst-sellers OR a new inventory-side endpoint.
- **Queries:** `Σ revenue`, `Σ qty`, margin = revenue − recipe cost, grouped by product.
- **KPIs:** Revenue, Units, Margin %, Menu-engineering quadrant (star/plowhorse/puzzle/dog).
- **Widgets:** quadrant scatter, ranked table.
- **Architectural impact:** **Medium** — overlaps existing `ComputeRankedProductsAsync`; mostly a margin join.
- **Dependencies:** Recipes for cost; reuse Financial calculators to avoid a 3rd "top products" definition.

## Category Performance
- **Objective:** Same as Product Performance, aggregated to `Category`.
- **Data sources:** `OrderItem → Product.CategoryId`.
- **APIs:** same endpoint, category grouping.
- **Queries:** group product performance by `CategoryId`.
- **KPIs:** Revenue/Units/Margin by category, category mix %.
- **Widgets:** category bar + share donut.
- **Architectural impact:** **Low** — grouping change on Product Performance.
- **Dependencies:** Product Performance.

## Supplier Analytics *(supported by current data model)*
- **Objective:** Spend, fill rate, and cost trend per supplier.
- **Data sources:** `Supplier`, `PurchaseOrder`, `PurchaseOrderItem`.
- **APIs:** new `GET /api/dashboards/inventory/suppliers` (or a procurement-scoped dashboard).
- **Queries:** `Σ PO value` by supplier, avg unit cost by material/supplier, on-time/received ratios.
- **KPIs:** Spend by supplier, Avg lead time, Price variance, PO count.
- **Widgets:** supplier ranked table, spend trend, price-variance heat.
- **Architectural impact:** **Medium** — new calculators over procurement tables; needs PO status/date fields verified.
- **Dependencies:** Procurement module completeness (PO lifecycle dates).

## Inventory Cost Trends
- **Objective:** Track unit-cost movement over time (purchase price drift, valuation changes).
- **Data sources:** `PurchaseOrderItem` unit costs over time; `StockBatch.UnitCost`.
- **APIs:** part of Supplier/Procurement endpoint.
- **Queries:** time series of weighted avg cost per material from PO receipts.
- **KPIs:** Cost trend per material, Inflation %, Cost vs menu-price spread.
- **Widgets:** multi-line cost trend, biggest movers table.
- **Architectural impact:** **Medium** — relies on PO history; **no historical `CostPerUnit` snapshots exist**, so trend must be sourced from PO receipts, not `RawMaterial`.
- **Dependencies:** Procurement history depth.

## Inventory Turnover
- **Objective:** How fast stock cycles (COGS ÷ average inventory value) — efficiency/overstock signal.
- **Data sources:** consumption/COGS + inventory value; **needs average inventory over the period**.
- **APIs:** inventory analytics endpoint.
- **Queries:** turnover = period COGS ÷ avg inventory value; **avg inventory requires periodic snapshots** (today only a live value exists).
- **KPIs:** Turnover ratio, Days-on-hand, per-category turnover.
- **Widgets:** turnover KPI + per-category bars.
- **Architectural impact:** **High** — requires a scheduled **inventory snapshot** job to build an average; cannot be computed accurately from a single live value.
- **Dependencies:** snapshot/ledger infrastructure.

## Fast-moving / Slow-moving Items
- **Objective:** Classify materials/products by velocity to guide purchasing.
- **Data sources:** consumption velocity (units/day) from derived consumption.
- **APIs:** consumption endpoint extension.
- **Queries:** rank by consumed qty ÷ days; bucket into fast/medium/slow by percentile.
- **KPIs:** Velocity per item, fast/slow lists.
- **Widgets:** two ranked lists + velocity distribution.
- **Architectural impact:** **Low–Medium** — derives from Consumption.
- **Dependencies:** Consumption Dashboard.

## Dead Stock
- **Objective:** Surface materials with stock but **zero consumption** over a window (capital tied up).
- **Data sources:** `RawMaterial.CurrentStock > 0` minus any consumption/waste in window.
- **APIs:** consumption endpoint extension.
- **Queries:** materials with stock and `consumption = 0 && waste = 0` over the period; value at risk.
- **KPIs:** Dead-stock count, Dead-stock value, Oldest no-movement age.
- **Widgets:** dead-stock table sorted by tied-up value.
- **Architectural impact:** **Medium** — needs reliable "last movement" date (best with a ledger).
- **Dependencies:** Consumption + ideally Stock Movement ledger.

## Inventory Forecasting (opportunity)
- **Objective:** Project run-out dates / suggested reorder quantities from consumption velocity.
- **Data sources:** consumption velocity + current stock + lead time (supplier).
- **APIs:** new forecasting endpoint (likely a separate service).
- **Queries:** days-to-stockout = CurrentStock ÷ avg daily consumption; reorder suggestion = velocity × lead time + safety stock.
- **KPIs:** Projected stockouts (next 7/14d), Suggested POs.
- **Widgets:** projected-stockout list, reorder suggestions.
- **Architectural impact:** **High** — forecasting logic + (later) seasonality; depends on clean consumption history and supplier lead times.
- **Dependencies:** Consumption, Supplier lead times; possible background job.

---

## Suggested sequencing
1. **Foundation:** decide Stock-movement read-model (UNION view vs `StockLedgerEntry`) + optional inventory snapshot job. Unblocks Movements, Turnover, Dead Stock, Adjustments.
2. **Quick wins (low impact, high value):** Expired Inventory (P0), Consumption, Category/Product Performance, Fast/Slow movers.
3. **Procurement-backed:** Supplier Analytics, Inventory Cost Trends.
4. **Advanced (needs foundation):** Stock Movements, Adjustments, Turnover, Dead Stock, Forecasting.

## Cross-cutting requirements (reuse, don't re-implement)
- Reuse `MetricQueryBuilder` window/scope + `OrderLifecyclePredicates` so new revenue/consumption numbers reconcile with existing dashboards.
- Any "top products"/COGS metric must reuse the Financial calculators — do **not** create a 4th definition.
- Bilingual (`Name`/`NameAr`), `decimal(18,2)`, guarded ratios, `SafeAsync` widgets, and the bilingual ClosedXML export pattern (`DashboardWorkbookWriter`) are mandatory to match the current dashboards.
