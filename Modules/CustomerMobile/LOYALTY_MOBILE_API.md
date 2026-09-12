# Loyalty (Marketing Module) — Mobile API

Customer-facing loyalty endpoints consumed by the mobile app. Scope: **loyalty/marketing only** — no other modules.

All endpoints are **read-only** except `POST /calculate` (a non-persisting preview). Points are **earned/redeemed by staff at the POS** — the mobile app **cannot** earn, redeem, or adjust points.

---

## Base & conventions

| | |
|---|---|
| Base path | `/api/mobile/loyalty` |
| Auth | Customer JWT (Bearer). `Authorization: Bearer <token>` |
| Required role | `Customer` |
| Tenant | Resolved from the JWT `tenant_id` claim (same token used by the rest of the mobile app). No extra header needed. |
| Rate limit | `api` policy (per authenticated caller). On exceed → `429 Too Many Requests`. |
| JSON casing | **camelCase** (e.g. `availablePoints`). |
| Money & points | JSON numbers, 2 decimals (e.g. `125.50`). |
| Dates | UTC, ISO-8601 (e.g. `2026-06-25T09:30:00Z`). |
| Enums | Returned as **strings** (see [Enum reference](#enum-reference)), not numbers. |
| Isolation | Every endpoint returns **only the authenticated customer's own data**. |

### Errors
| Status | Meaning |
|---|---|
| `401 Unauthorized` | Missing/expired/invalid token. |
| `403 Forbidden` | Token is valid but not a `Customer` role. |
| `400 Bad Request` | Validation failure (only `POST /calculate`). Body = ASP.NET `ModelState` problem object. |
| `404 Not Found` | Customer profile not resolvable from the token. |
| `429 Too Many Requests` | Rate limit hit. |
| `500` | Unexpected server error. |

### Pagination envelope
List endpoints return:
```json
{ "items": [ /* ... */ ], "totalCount": 134, "pageNumber": 1, "pageSize": 20 }
```
`page` starts at **1**. `pageSize` is clamped to **1–100** (default 20).

---

## 1. Loyalty summary

`GET /api/mobile/loyalty`

Headline numbers for the loyalty home screen.

**Response** `200` — `CustomerLoyaltySummaryDto`
| field | type | notes |
|---|---|---|
| customerId | guid | the mobile account id |
| availablePoints | number | redeemable now |
| availableBalance | number | `availablePoints × pointValue` (money) |
| tier | string | tier name, e.g. `Gold` ([tiers](#tier-values)) |
| tierAr | string | Arabic tier label |
| nextTier | string \| null | next tier name, or null if top tier |
| pointsToNextTier | number | points still needed for `nextTier` (0 if none) |
| lastUpdated | datetime | |

```json
{
  "customerId": "7d2f...e9",
  "availablePoints": 1250.00,
  "availableBalance": 12.50,
  "tier": "Silver",
  "tierAr": "فضي",
  "nextTier": "Gold",
  "pointsToNextTier": 3750.00,
  "lastUpdated": "2026-06-25T09:30:00Z"
}
```

---

## 2. Wallet snapshot

`GET /api/mobile/loyalty/wallet`

Full wallet breakdown for a wallet/points screen.

**Response** `200` — `CustomerLoyaltyWalletDto`
| field | type | notes |
|---|---|---|
| customerId | guid | |
| availablePoints | number | redeemable now |
| pendingPoints | number | earned but awaiting approval (not yet redeemable) |
| lifetimePoints | number | total ever earned |
| redeemedPoints | number | total ever redeemed |
| expiredPoints | number | total ever expired |
| availableBalance | number | money value of `availablePoints` |
| currencyCode | string | e.g. `JOD` |
| tier | string | [tiers](#tier-values) |
| tierAr | string | |
| status | string | `Active` \| `Frozen` \| `Closed` |

> If `status` ≠ `Active`, the customer cannot earn/redeem. A new customer with no wallet yet returns zeros + `Active`.

```json
{
  "customerId": "7d2f...e9",
  "availablePoints": 1250.00,
  "pendingPoints": 50.00,
  "lifetimePoints": 4300.00,
  "redeemedPoints": 3000.00,
  "expiredPoints": 0.00,
  "availableBalance": 12.50,
  "currencyCode": "JOD",
  "tier": "Silver",
  "tierAr": "فضي",
  "status": "Active"
}
```

---

## 3. Transaction history

`GET /api/mobile/loyalty/transactions?page=1&pageSize=20`

Paginated wallet ledger (newest first).

**Response** `200` — `Paginated<CustomerLoyaltyTransactionDto>`
| field | type | notes |
|---|---|---|
| id | guid | |
| type | string | [transaction types](#transaction-type) |
| points | number | **signed** — positive = credit, negative = debit |
| balanceAfter | number | available balance after this row |
| status | string | [transaction status](#transaction-status) |
| source | string | [transaction source](#transaction-source) |
| reason | string \| null | human-readable note (e.g. `Reward: Free Coffee`) |
| createdAt | datetime | |
| expiresAt | datetime \| null | when these points expire (null = never) |

```json
{
  "items": [
    { "id": "a1...", "type": "Redeem", "points": -100.00, "balanceAfter": 1250.00,
      "status": "Available", "source": "Reward", "reason": "Reward: Free Coffee",
      "createdAt": "2026-06-24T18:05:00Z", "expiresAt": null },
    { "id": "b2...", "type": "Earn", "points": 75.00, "balanceAfter": 1350.00,
      "status": "Available", "source": "Order", "reason": null,
      "createdAt": "2026-06-24T17:40:00Z", "expiresAt": "2027-06-24T17:40:00Z" }
  ],
  "totalCount": 134, "pageNumber": 1, "pageSize": 20
}
```

---

## 4. Reward catalog

`GET /api/mobile/loyalty/rewards`

Active, in-window rewards the customer can browse, with affordability for **this** customer.

**Response** `200` — `CustomerRewardDto[]` (plain array, not paginated)
| field | type | notes |
|---|---|---|
| id | guid | |
| name / nameAr | string / string? | |
| description / descriptionAr | string? / string? | |
| pointsRequired | number | cost in points |
| type | string | [reward types](#reward-type) |
| rewardValue | number \| null | fixed-discount amount, or percentage value, per `type` |
| maxDiscountAmount | number \| null | cap for `PercentageDiscount` |
| productId | guid \| null | for `FreeProduct` (references an existing product) |
| productName | string \| null | resolved product name for `FreeProduct` |
| affordable | bool | `true` if the customer currently has enough points |

```json
[
  { "id": "c3...", "name": "5 JOD off", "nameAr": "خصم 5 دينار", "description": null, "descriptionAr": null,
    "pointsRequired": 500.00, "type": "FixedDiscount", "rewardValue": 5.00,
    "maxDiscountAmount": null, "productId": null, "productName": null, "affordable": true },
  { "id": "d4...", "name": "Free Coffee", "nameAr": "قهوة مجانية", "description": null, "descriptionAr": null,
    "pointsRequired": 1500.00, "type": "FreeProduct", "rewardValue": null,
    "maxDiscountAmount": null, "productId": "f9...", "productName": "Espresso", "affordable": false }
]
```

> **Redemption is not done from mobile.** The customer shows their reward to a cashier, who redeems it at the POS. The catalog is display-only.

---

## 5. Reward redemption history

`GET /api/mobile/loyalty/rewards/redemptions?page=1&pageSize=20`

Paginated history of this customer's redeemed rewards (newest first).

**Response** `200` — `Paginated<CustomerRewardRedemptionDto>`
| field | type | notes |
|---|---|---|
| id | guid | |
| rewardName | string | name at redemption time |
| type | string | [reward types](#reward-type) |
| pointsUsed | number | points spent |
| discountAmount | number | money discount granted (0 for free product/delivery) |
| redeemedAt | datetime | |

```json
{
  "items": [
    { "id": "e5...", "rewardName": "Free Coffee", "type": "FreeProduct",
      "pointsUsed": 1500.00, "discountAmount": 0.00, "redeemedAt": "2026-06-20T12:10:00Z" }
  ],
  "totalCount": 3, "pageNumber": 1, "pageSize": 20
}
```

---

## 6. Redemption preview (calculator)

`POST /api/mobile/loyalty/calculate`

Non-persisting **preview** of how many points convert to how much discount for a hypothetical order. **Deducts nothing** — purely informational (e.g. an in-app "what if I use X points" slider). Subject to the tenant's min/max redemption limits.

**Request** — `CustomerLoyaltyCalculateRequest`
| field | type | rules |
|---|---|---|
| orderAmount | number | required, > 0 |
| pointsToUse | number | required, ≥ 0 |

**Response** `200` — `CustomerLoyaltyCalculationDto`
| field | type | notes |
|---|---|---|
| pointsUsed | number | echo of requested points |
| discountAmount | number | money value of those points |
| remainingPoints | number | available − pointsUsed |
| finalTotal | number | orderAmount − discountAmount |

```json
// request
{ "orderAmount": 30.00, "pointsToUse": 1000 }
// response
{ "pointsUsed": 1000.00, "discountAmount": 10.00, "remainingPoints": 250.00, "finalTotal": 20.00 }
```

`400` if points exceed available, below the min, above the max, or the resulting discount is invalid for the order.

---

## Enum reference

All enum-typed fields are returned as the **string** values below.

### Tier values
`Standard` · `Bronze` · `Silver` · `Gold` · `VIP`  (each has an Arabic label in `tierAr`)

### Wallet status
`Active` · `Frozen` · `Closed`

### Transaction type
`Earn` · `EarnPendingApproved` · `Redeem` · `Expire` · `ReverseEarn` · `ReverseRedeem` · `Adjust` · `ReferralBonus` · `CampaignBonus` · `MergeTransferOut` · `MergeTransferIn`

### Transaction status
`Pending` · `Available` · `Reversed` · `Expired`

### Transaction source
`Order` · `Manual` · `Referral` · `Campaign` · `Promo` · `Reward` · `Signup` · `Birthday` · `Merge`

### Reward type
`FixedDiscount` · `PercentageDiscount` · `FreeProduct` · `FreeDelivery`

---

## Integration notes
- **Auth:** reuse the existing customer login/refresh flow; the same Bearer token authorizes these endpoints. No separate loyalty login.
- **No write operations from mobile:** there is intentionally no earn/redeem/adjust endpoint. Don't build a redeem call — redemption happens at the POS.
- **`pendingPoints`** are not yet spendable; show them separately from `availablePoints`.
- **`affordable`** on the catalog already accounts for the customer's current points — use it to enable/disable the "show to cashier" CTA.
- **Signed `points`** in history: render credits/debits by sign; `balanceAfter` is the running available balance.
- **Empty/new customer:** all endpoints succeed with zeros/empty lists; handle gracefully.
- **Localization:** `*Ar` fields are provided for Arabic UI; fall back to the non-Ar field if an `*Ar` is null.

## Quick test (curl)
```bash
TOKEN="<customer jwt>"
BASE="https://<host>/api/mobile/loyalty"

curl -H "Authorization: Bearer $TOKEN" "$BASE"
curl -H "Authorization: Bearer $TOKEN" "$BASE/wallet"
curl -H "Authorization: Bearer $TOKEN" "$BASE/transactions?page=1&pageSize=20"
curl -H "Authorization: Bearer $TOKEN" "$BASE/rewards"
curl -H "Authorization: Bearer $TOKEN" "$BASE/rewards/redemptions?page=1&pageSize=20"
curl -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
     -d '{"orderAmount":30,"pointsToUse":1000}' "$BASE/calculate"
```
