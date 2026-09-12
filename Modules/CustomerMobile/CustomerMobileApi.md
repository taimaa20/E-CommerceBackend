# Mobile Integration Guide

> Source of truth: current `POS-Backend` controllers, DTOs, services, middleware, payment application/domain code, and Paymob adapter. Verified 2026-07-11. JSON property names are camelCase. Dates are ISO-8601 JSON strings; GUIDs are strings; monetary values are JSON numbers.

## 1. Introduction

The mobile client calls the ASP.NET Core HTTP API. Customer routes are under `/api/mobile`; hosted payment-engine routes are under `/api/v1/payment-engine`. The backend resolves tenant and branch context, controllers delegate to services or MediatR handlers, repositories persist through EF Core/PostgreSQL, and order updates can be published through `MobileOrderHub`.

The supported customer-mobile scope is authentication, home/menu discovery, profile and addresses, delivery-zone checking, cart, checkout/orders, cancellation/reorder/tracking, loyalty, devices, notifications, settings, and configured payment-method discovery. Staff mobile attendance and HR request APIs are a separate application scope and are not customer-app APIs.

Base URL: `https://localhost:7142`

Routes are under `/api/mobile`. Anonymous auth requests can pass `X-Tenant-ID: {tenantGuid}`. Authenticated customer requests use `Authorization: Bearer {accessToken}` and the token `tenant_id` claim.

Rate limits:
- Auth endpoints: `auth` policy
- Customer profile, cart, order, settings, loyalty, and notification endpoints: `api` policy

## API Table

| Method | Endpoint | Auth | Request | Response | Notes |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/mobile/auth` | Anonymous | `CustomerStartAuthRequest` | `CustomerStartAuthResponse` | Starts phone OTP; creates a stub customer when unknown. Current temporary OTP is `0000`. |
| POST | `/api/mobile/auth/verify` | Anonymous | `CustomerVerifyAuthRequest` | `CustomerVerifyAuthResponse` | Verifies OTP and returns JWT plus `COMPLETE_PROFILE` or `HOME`. |
| GET | `/api/mobile/customer/me` | Customer | None | `CustomerMeDto` | Current phone-auth customer identity/profile state. |
| PUT | `/api/mobile/customer/profile` | Customer | `CustomerCompleteProfileRequest` | `CustomerCompleteProfileResponse` | Completes the phone-auth profile. |
| GET | `/api/mobile/home?language=en` | Optional Bearer | Query | `CustomerHomeDto` | Single home payload: banners, categories, popular/featured products, offers, last orders when customer token is sent. |
| GET | `/api/mobile/search?keyword=burger&language=en` | Anonymous | Query | `CustomerSearchResponseDto` | Searches products, categories, and offers. |
| GET | `/api/mobile/products/{id}?language=en` | Anonymous | None | `PublicProductDto` | Mobile product details from public menu shape. |
| POST | `/api/mobile/auth/register` | Anonymous | `CustomerRegisterRequest` | `CustomerProfileDto` | Creates a customer account. Login after register to receive tokens. |
| POST | `/api/mobile/auth/login` | Anonymous | `LoginRequest` | Staff auth response or `CustomerLoginResponse` | Existing mobile login. Tries staff first, then customer by email/mobile. |
| POST | `/api/mobile/auth/refresh` | Anonymous | `RefreshTokenRequest` | Staff auth response or `CustomerLoginResponse` | Existing refresh endpoint with customer fallback. |
| POST | `/api/mobile/auth/refresh-token` | Anonymous | `CustomerRefreshTokenRequest` | `CustomerLoginResponse` | Customer-specific refresh endpoint. |
| POST | `/api/mobile/auth/logout` | Bearer | `LogoutRequest` | `{ message }` | Revokes staff/customer refresh token when supplied. |
| GET | `/api/mobile/auth/me` | Bearer | None | `AuthUserDto` | Generic mobile token identity endpoint. Use `/profile` for customer details. |
| POST | `/api/mobile/auth/forgot-password` | Anonymous | `CustomerForgotPasswordRequest` | `{ message }` | Sends reset OTP when provider is configured. |
| POST | `/api/mobile/auth/reset-password` | Anonymous | `CustomerResetPasswordRequest` | `{ message }` | Verifies reset OTP and updates password. |
| POST | `/api/mobile/auth/verify-otp` | Anonymous | `CustomerVerifyOtpRequest` | `{ message }` | Verifies customer account OTP. |
| POST | `/api/mobile/devices` | Customer | `CustomerDeviceRegisterRequest` | `CustomerDeviceDto` | Registers/updates device id, type, and FCM token. |
| POST | `/api/mobile/notifications/register` | Customer | `CustomerDeviceRegisterRequest` | `CustomerDeviceDto` | Push-token registration alias over the device API. |
| GET | `/api/mobile/notifications?page=1&pageSize=20` | Customer | Query | `PaginatedResponse<CustomerNotificationDto>` | Owned customer notifications only. |
| PUT | `/api/mobile/notifications/{id}/read` | Customer | None | `CustomerMobileActionResponse` | Marks an owned notification as read and writes audit log. |
| GET | `/api/mobile/profile` | Customer | None | `CustomerProfileDto` | Current customer profile. |
| PUT | `/api/mobile/profile` | Customer | `CustomerProfileUpdateRequest` | `CustomerProfileDto` | Updates name and preferred language. |
| GET | `/api/mobile/profile/addresses` | Customer | None | `CustomerAddressDto[]` | Current customer's saved addresses. |
| POST | `/api/mobile/profile/address` | Customer | `CustomerAddressRequest` | `CustomerAddressDto` | Adds address. `IsDefault=true` clears other defaults. |
| PUT | `/api/mobile/profile/address/{id}` | Customer | `CustomerAddressRequest` | `CustomerAddressDto` | Updates an owned address. |
| DELETE | `/api/mobile/profile/address/{id}` | Customer | None | `204 No Content` | Deletes an owned address. |
| DELETE | `/api/mobile/profile/account` | Customer | None | `CustomerMobileActionResponse` | Soft-deletes account, revokes tokens, disables devices, clears active carts. |
| GET | `/api/mobile/settings/follow-us` | Customer | None | `CustomerFollowUsDto` | Social links from admin-managed settings. |
| GET | `/api/mobile/settings/contact` | Customer | None | `CustomerContactDto` | Contact details from admin-managed settings. |
| GET | `/api/mobile/settings/terms` | Customer | None | `CustomerContentPageDto` | Terms content from admin-managed settings. |
| GET | `/api/mobile/settings/privacy-policy` | Customer | None | `CustomerContentPageDto` | Privacy content from admin-managed settings. |
| GET | `/api/mobile/loyalty` | Customer | None | `CustomerLoyaltySummaryDto` | Current CRM loyalty points and tier for this mobile customer. |
| POST | `/api/mobile/loyalty/calculate` | Customer | `CustomerLoyaltyCalculateRequest` | `CustomerLoyaltyCalculationDto` | Server-side loyalty redemption preview. |
| POST | `/api/mobile/delivery/check-zone` | Customer | `CustomerDeliveryCheckRequest` | `CustomerDeliveryCheckResponse` | Finds active delivery zone by configured center/radius. |
| GET | `/api/mobile/cart` | Customer | None | `CustomerCartDto` | Active customer cart with recalculated prices. |
| POST | `/api/mobile/cart/items` | Customer | `CustomerCartItemRequest` | `CustomerCartDto` | Adds item to cart. |
| PUT | `/api/mobile/cart/items/{id}` | Customer | `CustomerCartItemUpdateRequest` | `CustomerCartDto` | Updates quantity, notes, modifiers. |
| DELETE | `/api/mobile/cart/items/{id}` | Customer | None | `CustomerCartDto` | Removes item and returns cart. |
| DELETE | `/api/mobile/cart/clear` | Customer | None | `204 No Content` | Clears active cart. |
| GET | `/api/mobile/payment-methods` | Customer | None | `PaymentMethodDto[]` | Active checkout payment methods. |
| POST | `/api/mobile/orders/calculate` | Customer | `CustomerOrderCalculateRequest` | `CustomerOrderCalculationResponse` | Server-side checkout calculation. |
| POST | `/api/mobile/orders` | Customer | `CustomerOrderCreateRequest` | `CustomerOrderCreateResponse` | Creates POS order, kitchen notification, and print event. |
| GET | `/api/mobile/orders?page=1&pageSize=20` | Customer | Query | `PaginatedResponse<CustomerOrderListItemDto>` | `page` min 1. `pageSize` clamped to 1-50. |
| GET | `/api/mobile/orders/{id}` | Customer | None | `CustomerOrderDetailsDto` | Gets one owned mobile order. |
| GET | `/api/mobile/orders/{id}/tracking` | Customer | None | `CustomerOrderTrackingDto` | Current status, ETA, and derived timeline. |
| POST | `/api/mobile/orders/{id}/reorder` | Customer | None | `CustomerCartDto` | Copies a previous order into the active cart after validating current product availability. |
| POST | `/api/mobile/orders/{id}/cancel` | Customer | `CustomerCancelOrderRequest` | `CustomerCancelOrderResponse` | Cancels owned mobile order when status is `New`, or `Preparing` when `AllowMobileCancelPreparing=true`. |

SignalR:

| Hub | Auth | Events | Notes |
| --- | --- | --- | --- |
| `/mobile-order-hub` | Customer | `OrderPlaced`, `Preparing`, `Ready`, `OutForDelivery`, `Delivered`, `Cancelled` | `SubscribeToOrder(orderId)` only joins orders owned by the current customer. Current status polling remains `/tracking`; POS-wide status-change broadcasting still needs Phase 2 integration. |

## Common Headers

```http
X-Tenant-ID: 00000000-0000-0000-0000-000000000000
Authorization: Bearer {accessToken}
Content-Type: application/json
```

`X-Tenant-ID` is important for anonymous requests when the app should not use the default tenant fallback.

## Home/Search DTOs

### CustomerHomeDto

```json
{
  "banners": [
    {
      "title": "Today's Specials",
      "titleAr": "عروض اليوم",
      "imageUrl": "/uploads/banner.webp"
    }
  ],
  "categories": [],
  "popularProducts": [],
  "featuredProducts": [],
  "availableOffers": [],
  "lastOrders": []
}
```

`categories`, `popularProducts`, and product details use the existing public menu DTOs. `lastOrders` is empty unless a valid customer bearer token is sent.

### CustomerSearchResponseDto

```json
{
  "products": [],
  "categories": [],
  "offers": []
}
```

## Auth DTOs

### CustomerRegisterRequest

```json
{
  "firstName": "Ali",
  "lastName": "Hassan",
  "email": "ali@example.com",
  "mobileNumber": "+97455555555",
  "password": "Password123!",
  "preferredLanguage": "en"
}
```

Rules:
- `firstName`, `lastName`: required, 1-100 chars
- `email`: required, valid email, max 150 chars, unique per tenant
- `mobileNumber`: required, 3-20 chars, unique per tenant
- `password`: required, 8-128 chars, must include uppercase, lowercase, number, and special character
- `preferredLanguage`: optional, `en` or `ar`; any value other than `ar` becomes `en`

Response: `CustomerProfileDto`. Call `POST /api/mobile/auth/login` after registration to receive `accessToken` and `refreshToken`.

### LoginRequest

```json
{
  "username": "ali@example.com",
  "password": "Password123!",
  "deviceId": "iphone-15-ali",
  "deviceName": "Ali iPhone"
}
```

For customer login, `username` can be customer email or mobile number. This endpoint still supports staff login.

### CustomerLoginRequest

Used internally by the customer fallback and useful for documenting the customer-specific shape:

```json
{
  "identifier": "ali@example.com",
  "password": "Password123!",
  "deviceId": "iphone-15-ali",
  "deviceName": "Ali iPhone",
  "deviceType": "Ios",
  "fcmToken": "fcm-token"
}
```

`deviceType` accepts `Unknown`, `Ios`, `Android`, or `Web`. Invalid values become `Unknown`.

### CustomerLoginResponse

```json
{
  "accessToken": "jwt",
  "refreshToken": "refresh-token",
  "accessTokenExpiresAt": "2026-06-04T10:15:00Z",
  "refreshTokenExpiresAt": "2026-06-04T18:00:00Z",
  "customer": {
    "id": "00000000-0000-0000-0000-000000000000",
    "customerNumber": "C2606041234",
    "firstName": "Ali",
    "lastName": "Hassan",
    "email": "ali@example.com",
    "mobileNumber": "+97455555555",
    "preferredLanguage": "en",
    "isVerified": true,
    "createdAt": "2026-06-04T10:00:00Z",
    "lastLoginDate": "2026-06-04T10:00:00Z"
  }
}
```

### RefreshTokenRequest

```json
{
  "refreshToken": "refresh-token",
  "deviceId": "iphone-15-ali"
}
```

Refresh token rotation is enabled. Reusing an old rotated token revokes customer tokens.

### LogoutRequest

```json
{
  "refreshToken": "refresh-token"
}
```

### CustomerForgotPasswordRequest

```json
{
  "identifier": "ali@example.com"
}
```

### CustomerResetPasswordRequest

```json
{
  "identifier": "ali@example.com",
  "otpCode": "123456",
  "newPassword": "NewPassword123!"
}
```

### CustomerVerifyOtpRequest

```json
{
  "identifier": "ali@example.com",
  "otpCode": "123456"
}
```

OTP depends on `ICustomerOtpProvider`. The current provider is `NoOpCustomerOtpProvider`, so real OTP send/verify needs a provider implementation before enabling `CustomerMobile:RequireOtpVerification=true`.

## Device/Notification DTOs

### CustomerDeviceRegisterRequest

```json
{
  "deviceId": "iphone-15-ali",
  "deviceType": "Ios",
  "fcmToken": "fcm-token"
}
```

`deviceType` accepts `Unknown`, `Ios`, `Android`, or `Web`. Invalid values become `Unknown`.

### CustomerDeviceDto

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "deviceId": "iphone-15-ali",
  "deviceType": "Ios",
  "hasFcmToken": true,
  "lastSeenAt": "2026-06-04T10:00:00Z"
}
```

### CustomerNotificationDto

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "title": "Order update",
  "titleAr": "تحديث الطلب",
  "message": "Your order is ready",
  "messageAr": "طلبك جاهز",
  "type": "Order",
  "isRead": false,
  "createdAt": "2026-06-04T10:00:00Z"
}
```

Notification `type` is normalized for the mobile app as `Order`, `Promotion`, `Loyalty`, or `General`. The list endpoint only returns rows where `CustomerId` matches the current customer token.

### CustomerMobileActionResponse

```json
{
  "success": true,
  "message": "Notification marked as read."
}
```

## Profile DTOs

### CustomerProfileUpdateRequest

```json
{
  "firstName": "Ali",
  "lastName": "Hassan",
  "preferredLanguage": "ar"
}
```

### CustomerAddressRequest

```json
{
  "addressName": "Home",
  "area": "Doha",
  "street": "Street 10",
  "building": "12",
  "floor": "2",
  "apartment": "8",
  "latitude": 25.2854,
  "longitude": 51.531,
  "deliveryZoneId": "00000000-0000-0000-0000-000000000000",
  "isDefault": true
}
```

Rules:
- `addressName`: required, 1-100 chars
- `area`: required, 1-120 chars
- `street`: required, 1-200 chars
- `deliveryZoneId`: optional, but if supplied it must reference an active delivery zone
- delivery orders require an address with `deliveryZoneId`

### CustomerAddressDto

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "addressName": "Home",
  "area": "Doha",
  "street": "Street 10",
  "building": "12",
  "floor": "2",
  "apartment": "8",
  "latitude": 25.2854,
  "longitude": 51.531,
  "deliveryZoneId": "00000000-0000-0000-0000-000000000000",
  "isDefault": true,
  "createdAt": "2026-06-04T10:00:00Z"
}
```

### Delete Account

`DELETE /api/mobile/profile/account` has no body.

Response:

```json
{
  "success": true,
  "message": "Account deleted successfully."
}
```

The account is soft-deleted, active refresh tokens are revoked, FCM tokens are cleared, devices are disabled, active carts are closed, and a `CustomerMobileAuditLog` row is written.

## Delivery DTOs

### CustomerDeliveryCheckRequest

```json
{
  "latitude": 25.2854,
  "longitude": 51.531
}
```

### CustomerDeliveryCheckResponse

```json
{
  "isAvailable": true,
  "zone": {
    "id": "00000000-0000-0000-0000-000000000000",
    "nameEn": "Doha Central",
    "nameAr": "وسط الدوحة",
    "code": "DOHA_CENTRAL",
    "deliveryFee": 10.00,
    "paymentMode": "CustomerPays"
  },
  "deliveryFee": 10.00,
  "message": null
}
```

Delivery zone coordinate matching uses these nullable fields on `DeliveryZone`:
- `CenterLatitude`
- `CenterLongitude`
- `RadiusMeters`

If active zones do not have these fields configured, the endpoint returns `isAvailable=false` with `Coordinate-based delivery zones are not configured.`

## Cart DTOs

### CustomerCartItemRequest

```json
{
  "productId": "00000000-0000-0000-0000-000000000000",
  "quantity": 2,
  "selectedOptionId": "00000000-0000-0000-0000-000000000000",
  "notes": "No onions",
  "modifiers": [
    {
      "modifierId": "00000000-0000-0000-0000-000000000000",
      "quantity": 1
    }
  ]
}
```

Rules:
- `quantity`: 1-99
- `productId`: must be an active product
- `selectedOptionId`: optional, must belong to the product
- `modifierId`: must be valid for the product

### CustomerCartItemUpdateRequest

```json
{
  "quantity": 1,
  "notes": "Extra sauce",
  "modifiers": []
}
```

### CustomerCartDto

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "subtotal": 30.00,
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "productId": "00000000-0000-0000-0000-000000000000",
      "productName": "Burger",
      "productNameAr": "برجر",
      "quantity": 2,
      "unitPrice": 12.00,
      "lineTotal": 30.00,
      "selectedOptionId": null,
      "selectedOptionName": null,
      "selectedOptionNameAr": null,
      "notes": "No onions",
      "modifiers": [
        {
          "modifierId": "00000000-0000-0000-0000-000000000000",
          "modifierName": "Cheese",
          "modifierNameAr": "جبن",
          "price": 3.00,
          "quantity": 1
        }
      ]
    }
  ]
}
```

Cart prices are recalculated from current product, option, and modifier data when the cart is returned.

## Order DTOs

### CustomerOrderCalculateRequest

```json
{
  "orderType": "Delivery",
  "deliveryAddressId": "00000000-0000-0000-0000-000000000000",
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "quantity": 2,
      "selectedOptionId": null,
      "notes": "No onions",
      "modifiers": [
        {
          "modifierId": "00000000-0000-0000-0000-000000000000",
          "quantity": 1
        }
      ]
    }
  ]
}
```

### CustomerOrderCalculationResponse

```json
{
  "subtotal": 30.00,
  "discounts": 0.00,
  "tax": 0.00,
  "deliveryFee": 10.00,
  "serviceFee": 0.00,
  "finalTotal": 40.00,
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "productName": "Burger",
      "quantity": 2,
      "unitPrice": 12.00,
      "modifiersTotal": 3.00,
      "lineTotal": 30.00
    }
  ]
}
```

Current mobile order creation stores `DiscountAmount`, `TaxAmount`, and `ServiceChargeAmount` as zero, so the calculation endpoint mirrors that behavior until the order engine is extracted/shared.

### CustomerOrderCreateRequest

```json
{
  "orderType": "Delivery",
  "deliveryAddressId": "00000000-0000-0000-0000-000000000000",
  "paymentMethod": "Cash",
  "notes": "Call on arrival",
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "quantity": 2,
      "selectedOptionId": null,
      "notes": "No onions",
      "modifiers": [
        {
          "modifierId": "00000000-0000-0000-0000-000000000000",
          "quantity": 1
        }
      ]
    }
  ]
}
```

Rules:
- `orderType`: `Pickup` or `Delivery`
- `items`: at least one item
- item `quantity`: 1-99
- delivery orders require `deliveryAddressId`
- delivery address must belong to the customer and have an active `deliveryZoneId`
- delivery fee/cost/payment mode are snapshotted from the delivery zone

### CustomerOrderCreateResponse

```json
{
  "orderId": "00000000-0000-0000-0000-000000000000",
  "orderNumber": "MD-260604-123456",
  "status": "Pending"
}
```

Order numbers use `MP-` for pickup and `MD-` for delivery.

### PaginatedResponse<CustomerOrderListItemDto>

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "orderId": "00000000-0000-0000-0000-000000000000",
      "orderNumber": "MD-260604-123456",
      "orderType": "Delivery",
      "status": "Pending",
      "totalAmount": 45.00,
      "createdAt": "2026-06-04T10:00:00Z"
    }
  ],
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 20
}
```

### CustomerOrderDetailsDto

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "orderId": "00000000-0000-0000-0000-000000000000",
  "orderNumber": "MD-260604-123456",
  "orderType": "Delivery",
  "status": "Pending",
  "totalAmount": 45.00,
  "createdAt": "2026-06-04T10:00:00Z",
  "deliveryAddress": "Home, Doha, Street 10, Building 12, Floor 2, Apartment 8",
  "deliveryNotes": "Call on arrival",
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "productName": "Burger",
      "quantity": 2,
      "unitPrice": 12.00,
      "lineTotal": 24.00
    }
  ]
}
```

### CustomerOrderTrackingDto

```json
{
  "orderId": "00000000-0000-0000-0000-000000000000",
  "orderNumber": "MD-260604-123456",
  "currentStatus": "Preparing",
  "eta": "2026-06-04T10:30:00Z",
  "timeline": [
    {
      "status": "OrderPlaced",
      "label": "Order Placed",
      "isCompleted": true,
      "isCurrent": false,
      "occurredAt": "2026-06-04T10:00:00Z"
    },
    {
      "status": "Preparing",
      "label": "Preparing",
      "isCompleted": true,
      "isCurrent": true,
      "occurredAt": "2026-06-04T10:05:00Z"
    }
  ]
}
```

The tracking timeline is derived from the existing `OrderStatus`, item readiness, and order timestamps. There is no dedicated mobile order-status audit table yet.

### Reorder

`POST /api/mobile/orders/{id}/reorder` has no body. It replaces the active cart with previous order items after validating current product, option, and modifier availability.

### CustomerCancelOrderRequest

```json
{
  "reason": "Customer requested cancellation"
}
```

### CustomerCancelOrderResponse

```json
{
  "success": true,
  "status": "Cancelled"
}
```

Rules:
- order must belong to the authenticated customer
- `New` orders can be cancelled
- `Preparing` orders can be cancelled only when `SystemSettings.AllowMobileCancelPreparing=true`
- paid, served, completed, or already cancelled orders are rejected
- successful cancellation updates the POS order and writes a `CustomerMobileAuditLog` row

## Settings DTOs

All settings values are read from `SystemSettings` and can be managed through `PUT /api/settings`.

### CustomerFollowUsDto

```json
{
  "facebook": "https://facebook.com/restaurant",
  "instagram": "https://instagram.com/restaurant",
  "tikTok": "https://tiktok.com/@restaurant",
  "snapchat": "https://snapchat.com/add/restaurant",
  "x": "https://x.com/restaurant",
  "youTube": "https://youtube.com/@restaurant",
  "website": "https://restaurant.example.com",
  "googleMaps": "https://maps.google.com/?q=restaurant"
}
```

### CustomerContactDto

```json
{
  "phone": "+97444444444",
  "whatsApp": "+97455555555",
  "email": "info@example.com",
  "workingHours": "10:00 AM - 11:00 PM",
  "address": "Doha",
  "addressAr": "الدوحة"
}
```

### CustomerContentPageDto

```json
{
  "title": "Terms & Conditions",
  "titleAr": "الشروط والأحكام",
  "content": "Terms content managed by admin.",
  "contentAr": "محتوى الشروط من لوحة الإدارة.",
  "lastUpdated": "2026-06-04T10:00:00Z"
}
```

## Loyalty DTOs

### CustomerLoyaltySummaryDto

```json
{
  "customerId": "00000000-0000-0000-0000-000000000000",
  "availablePoints": 250,
  "availableBalance": 25.00,
  "tier": "Silver",
  "tierAr": "فضي",
  "nextTier": "Gold",
  "pointsToNextTier": 250,
  "lastUpdated": "2026-06-04T10:00:00Z"
}
```

Mobile loyalty uses the CRM `Customer` row matched by mobile number. If no CRM customer exists yet, points return as `0`.

### CustomerLoyaltyCalculateRequest

```json
{
  "orderAmount": 45.00,
  "pointsToUse": 100
}
```

### CustomerLoyaltyCalculationDto

```json
{
  "pointsUsed": 100,
  "discountAmount": 10.00,
  "remainingPoints": 150,
  "finalTotal": 35.00
}
```

Rules:
- loyalty must be enabled in `SystemSettings.EnableLoyalty`
- `LoyaltyPointValue` must be configured
- requested points cannot exceed available points
- optional min/max redemption rules come from `LoyaltyMinimumRedeemPoints` and `LoyaltyMaximumRedeemPoints`
- calculation writes a `CustomerMobileAuditLog` row but does not consume points

## Error Responses

The backend uses the existing global exception middleware. Common results:

| Status | Cause |
| --- | --- |
| 400 | Invalid model state or validation failure. |
| 401 | Invalid credentials, expired session, invalid customer session, OTP failure. |
| 404 | Owned profile/address/cart/order resource was not found. |
| 409 | Email or mobile number already registered. |

## Mobile Flow

## 7. Payment Engine

### Integration boundary

The customer order API accepts `paymentMethod` as a string, but it does not call the payment engine. The current payment creation endpoint is authorized for `AppRoleGroups.TakeawayCheckoutOperators`, not the `Customer` role. Therefore direct customer-token payment initiation is **Not implemented in current backend.** A mobile app cannot complete the Paymob flow using only a customer JWT.

```text
Customer -> Mobile App -> Customer order API
                             |
                             +-> payment initiation with Customer JWT: Not implemented

Authorized takeaway operator -> Payment Engine -> Paymob intention
Paymob -> signed webhook -> Payment Engine -> payment/order update
```

There is no redirect-callback controller. The provider checkout URL is returned by payment creation, and completion is accepted through the webhook only. Redirect callback processing is **Not implemented in current backend.**

### Create a payment

`POST /api/v1/payment-engine/payments` (alias: `/api/payment-engine/payments`)

Authorization: Bearer token whose role belongs to `TakeawayCheckoutOperators`. Tenant and branch come from server context. `Content-Type: application/json`. A request correlation ID may be supplied in the body; otherwise `HttpContext.TraceIdentifier` is used.

Request schema:

| Property | Type | Nullable | Required/default | Validation |
| --- | --- | --- | --- | --- |
| `orderId` | GUID | No | Required | Non-empty enforced by command validator |
| `merchantReference` | string | No | Required | Trimmed; max is domain `MerchantReference.MaxLength`; nonblank |
| `amount` | decimal | No | Required | 0.01–9999999999999999.99 |
| `currency` | string | No | Required | Exactly 3 characters; normalized by domain |
| `method` | number | No | Default `0` | `PaymentMethod` enum |
| `providerCode` | string | No | Required | Nonblank; gateway provider-code max length |
| `idempotencyKey` | string | No | Required | Nonblank; max 200 |
| `billingData` | object | No | Required | See below |
| `customer` | object | Yes | Optional | See below |
| `items` | array | No | Default `[]` | Each item validated |
| `correlationId` | string | Yes | Optional | Max 200; trimmed when supplied |

`billingData`: required `firstName`, `lastName`, valid `email`, `phoneNumber`, and two-character `country`; optional nullable `city`, `state`, `postalCode`, `street`, `building`, `floor`, `apartment`. Text fields max 200 except phone max 40.

`customer`: nullable; when present, required `firstName`, `lastName`, and valid `email`, each max 200.

`items[]`: required `name` (max 200), `amount` 0.01–9999999999999999.99, `quantity` 1–2147483647 (default 1), nullable `description` max 200, nullable `imageUrl` max 2048.

```json
{
  "orderId": "11111111-1111-1111-1111-111111111111",
  "merchantReference": "ORDER-1001",
  "amount": 12.5,
  "currency": "QAR",
  "method": 0,
  "providerCode": "paymob",
  "idempotencyKey": "checkout-11111111-attempt-1",
  "billingData": {
    "firstName": "Mona",
    "lastName": "Ali",
    "email": "mona@example.com",
    "phoneNumber": "+97450000000",
    "country": "QA",
    "city": "Doha",
    "state": null,
    "postalCode": null,
    "street": null,
    "building": null,
    "floor": null,
    "apartment": null
  },
  "customer": null,
  "items": [{"name":"Burger","amount":12.5,"quantity":1,"description":null,"imageUrl":null}],
  "correlationId": "mobile-checkout-1001"
}
```

Success is HTTP 200 and serializes `CreatePaymentResult`: `paymentId`, `orderId`, `merchantReference`, `status`, `attemptStatus`, nullable `gatewayReference`, nullable `checkoutUrl`, `isReused`, and `correlationId`. Enum values use the application's default ASP.NET serializer behavior: numeric values unless runtime JSON options outside the inspected code add a string converter; no such converter is configured in `Program.cs`.

```json
{
  "paymentId": "22222222-2222-2222-2222-222222222222",
  "orderId": "11111111-1111-1111-1111-111111111111",
  "merchantReference": "ORDER-1001",
  "status": 1,
  "attemptStatus": 2,
  "gatewayReference": "provider-reference",
  "checkoutUrl": "https://provider.example/checkout/session",
  "isReused": false,
  "correlationId": "mobile-checkout-1001"
}
```

Idempotency is scoped by tenant, branch, provider, and idempotency key. Repeating the same accepted key returns the existing result with `isReused: true`; it does not create a second attempt. An active reusable hosted session may also be returned. Expired sessions are not reusable. Gateway transient retries are performed by the Paymob resilience handler; attempts remain separately auditable. Exact retry delays are configuration-driven.

Errors: null body returns `400 {"error":"Request body is required."}`. Data-annotation/`ApiController` validation returns HTTP 400 `ValidationProblemDetails`. Missing/invalid authentication returns 401; an authenticated role outside the policy returns 403. Conflicting merchant/idempotency/order state returns 409 through the typed exception pipeline. Provider failures are mapped by the payment gateway exception handling. No endpoint-specific 404 response is declared for creation.

### Query payment

`GET /api/v1/payment-engine/payments/{paymentId}` requires `TakeawayCheckoutOperators`. HTTP 200 returns:

```json
{
  "paymentId": "22222222-2222-2222-2222-222222222222",
  "orderId": "11111111-1111-1111-1111-111111111111",
  "branchId": "33333333-3333-3333-3333-333333333333",
  "merchantReference": "ORDER-1001",
  "providerCode": "paymob",
  "gatewayReference": null,
  "status": 0,
  "method": 0,
  "amount": 12.5,
  "currency": "QAR",
  "capturedAmount": 0,
  "createdAt": "2026-07-11T10:00:00Z",
  "updatedAt": "2026-07-11T10:00:00Z",
  "expiresAtUtc": null,
  "completedAtUtc": null,
  "activeCheckoutUrl": null,
  "attempts": [],
  "sessions": []
}
```

`attempts[]`: `attemptId`, `attemptNumber`, `operation`, `status`, `amount`, `currency`, nullable `gatewayProvider`, `gatewayReference`, `failureCode`, `failureMessage`, plus creation/start/completion timestamps defined by `PaymentEngineAttemptDto`.

`sessions[]`: `sessionId`, `status`, `providerCode`, nullable `gatewaySessionId`, `checkoutUrl`, `expiresAtUtc`, and creation/update timestamps defined by `PaymentEngineSessionDto`.

Responses: 200, 401, 403, or 404 `{"error":"..."}`.

### Query all payments for an order

`GET /api/v1/payment-engine/orders/{orderId}/payment` has the same authorization. It returns `{"orderId":"...","payments":[PaymentEnginePaymentDetailsDto],"currentPayment":PaymentEnginePaymentDetailsDto|null}`. Responses: 200, 401, 403, 404.

## 8. Payment Callback and Webhook

### Paymob webhook

`POST /api/v1/payment-engine/webhooks/paymob` is anonymous and consumes JSON. Maximum controller request size is 1,048,576 bytes; the effective configured `MaxWebhookBodyBytes` may be lower.

The raw Paymob transaction can be the root object, `obj`, or `transaction`. It must contain a transaction `id`. The mapper—not a public request DTO—reads Paymob's actual provider payload. Consequently a stable client-authored webhook request model is **Not implemented in current backend.**

HMAC is required when `RequireWebhookHmac` is true. The signature is resolved from the configured header, `X-Paymob-Hmac`, `X-HMAC-SHA512`, or `hmac`, then from the configured query parameter, `hmac`, or `signature`. The verifier concatenates configured transaction fields in configured order and computes HMAC-SHA512 with the configured secret; comparison is fixed-time. `created_at` must parse as a timestamp and fall inside `WebhookToleranceSeconds` when tolerance is positive.

Response model for every handled outcome:

```json
{
  "status": "accepted",
  "correlationId": "provider-or-http-correlation-id",
  "reason": null,
  "paymentId": "22222222-2222-2222-2222-222222222222",
  "isDuplicate": false
}
```

Status codes: 200 accepted (including safely recognized duplicates, flagged by `isDuplicate`); 400 unsupported/malformed payload; 401 missing/invalid HMAC or replay-window violation; 404 payment/reference not found; 409 conflicting event/state; 413 oversized payload; 503 provider disabled or HMAC configuration missing. Correlation ID preference is Paymob's configured correlation header, `X-Correlation-ID`, `X-Request-ID`, then ASP.NET trace ID.

Webhook payload SHA-256 hashes and provider event identity are persisted for duplicate detection. Valid completion events transition the payment and dispatch the payment-completed domain event; the order-payment integration records settlement and applies the configured preparation policy. Duplicate events do not apply the order update twice.

Redirect callback: **Not implemented in current backend.**

## 9. Order Tracking

`GET /api/mobile/orders/{id}/tracking` returns `CustomerOrderTrackingDto` documented above. The timeline service exposes the statuses `Pending`, `Confirmed`, `Preparing`, `Ready`, `Completed`, and `Cancelled` when applicable. `currentStatus` is the persisted order status string. Exact allowed customer cancellation states are enforced by `CustomerOrderCancellationService`; rejected transitions return 400/409 through typed exceptions. Arbitrary status mutation by a customer endpoint is **Not implemented in current backend.**

## 10. Notifications

Push-token registration is available at `POST /api/mobile/devices` and its alias `POST /api/mobile/notifications/register`. The backend stores device ID/type and an FCM token, exposes paginated owned notifications, and permits marking an owned notification read. Delivery through an external push provider is **Not implemented in current backend.** Order status real-time messages are published through `MobileOrderHub`; clients authenticate using the customer JWT.

## 11. Error Handling

Service exceptions serialize as:

```json
{"error":"Resource was not found."}
```

`ValidationException` adds `errors` and, when supplied, `status`:

```json
{"error":"Validation failed.","errors":{"field":["Reason"]},"status":"CurrentStatus"}
```

ASP.NET model validation uses `ValidationProblemDetails`:

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"field":["The field is required."]},"traceId":"..."}
```

Typed mappings are 400 validation, 401 unauthorized, 403 forbidden, 404 not found, and 409 conflict. Unexpected exceptions return 500 `{"error":"An unexpected error occurred while processing your request."}`. Authentication middleware may produce an empty 401/403 body; clients must not require an error JSON body for those statuses. Endpoint-inapplicable error shapes requested by a generic test matrix are not fabricated.

## 12. Models

The property tables and JSON examples in the preceding DTO sections are the mobile contract. Nullable C# properties are marked nullable; initialized collections serialize as arrays, normally `[]`; initialized strings serialize as strings, normally `""`. Non-nullable value types serialize with their CLR default when omitted unless validation/business logic rejects them. Inherited `CustomerOrderDetailsDto` includes every `CustomerOrderListItemDto` property plus its declared properties.

## 13. Payment Enums

Enums serialize numerically in the inspected configuration.

| Enum | Values |
| --- | --- |
| `PaymentStatus` | `Pending=0`, `RequiresAction=1`, `Authorized=2`, `Captured=3`, `PartiallyRefunded=4`, `Refunded=5`, `Voided=6`, `Failed=7`, `Cancelled=8`, `Expired=9` |
| `PaymentSessionStatus` | `Created=0`, `Pending=1`, `RequiresAction=2`, `Completed=3`, `Failed=4`, `Cancelled=5`, `Expired=6` |
| `PaymentAttemptStatus` | `Created=0`, `Processing=1`, `RequiresAction=2`, `Succeeded=3`, `Failed=4`, `Cancelled=5`, `TimedOut=6`, `Duplicate=7` |
| `PaymentMethod` | `Card=0`, `Wallet=1`, `Cash=2`, `BankTransfer=3`, `Other=4` |
| `PaymentOperation` | `Purchase=0`, `Authorize=1`, `Capture=2`, `Refund=3`, `Void=4` |
| `PaymentFailureReason` | `None=0`, `AuthenticationFailed=1`, `ValidationFailed=2`, `InsufficientFunds=3`, `Timeout=4`, `RateLimited=5`, `ProviderUnavailable=6`, `Duplicate=7`, `FraudSuspected=8`, `Unknown=9` |

Order, customer, loyalty, reward, and notification statuses in mobile DTOs are strings populated by services/entities, not JSON enums. A single exhaustive enum definition for those strings is **Not implemented in current backend.**

## 14. Consolidated Business Rules

- A customer can create, view, reorder, track, or request cancellation only for orders owned by that authenticated customer and tenant.
- Checkout calculation is server-authoritative. Product/option/modifier existence, availability, quantities, prices, order type, delivery address/zone, taxes, discounts, delivery fee, and service fee are recalculated by services; client totals are not accepted in order creation.
- `Delivery` requires a valid owned delivery address/zone; pickup does not use a delivery address.
- Payment creation validates the persisted order, currency/amount, provider, and merchant reference. Payment idempotency prevents a second operation for the same scoped key.
- A captured payment cannot be captured a second time by a duplicate webhook. Provider events are stored and duplicates are acknowledged without repeating side effects.
- A failed/expired hosted session is not reused. A new retry needs a new idempotency key unless the original request is being safely replayed to retrieve its existing result.
- Payment becomes paid/captured only from a verified provider outcome processed by the orchestrator. A browser redirect alone does not mark an order paid.
- Refund/void public endpoints are **Not implemented in current backend.** Their enum/domain foundations do not constitute callable APIs.
- Customer direct Paymob initiation and redirect callback handling are **Not implemented in current backend.**

## 15. Testing Guide

Set Postman variables `baseUrl`, `tenantId`, `accessToken`, `orderId`, and (for operator-only payment tests) `operatorToken`. Use `Content-Type: application/json`, `Accept: application/json`, `X-Tenant-ID: {{tenantId}}` for anonymous tenant resolution, and `Authorization: Bearer {{accessToken}}` for customer routes.

Phone authentication:

```http
POST {{baseUrl}}/api/mobile/auth
Content-Type: application/json
X-Tenant-ID: {{tenantId}}

{"phoneNumber":"+97450000000","deviceId":"postman","deviceName":"Postman"}
```

In the current temporary implementation, verify with OTP `0000`:

```http
POST {{baseUrl}}/api/mobile/auth/verify
Content-Type: application/json
X-Tenant-ID: {{tenantId}}

{"phoneNumber":"+97450000000","otp":"0000","deviceId":"postman","deviceName":"Postman","deviceType":"test","fcmToken":null}
```

Calculate before order creation:

```http
POST {{baseUrl}}/api/mobile/orders/calculate
Authorization: Bearer {{accessToken}}
Content-Type: application/json

{"orderType":"Pickup","deliveryAddressId":null,"items":[{"productId":"44444444-4444-4444-4444-444444444444","quantity":1,"selectedOptionId":null,"notes":null,"modifiers":[]}]}
```

Payment-engine smoke test (operator token, not customer token):

```http
GET {{baseUrl}}/api/v1/payment-engine/orders/{{orderId}}/payment
Authorization: Bearer {{operatorToken}}
Accept: application/json
```

Negative tests: omit required fields to assert 400 `ValidationProblemDetails`; omit bearer token to assert 401; use a customer token on payment-engine routes to assert 403; query another customer's order to assert 404; replay the same payment idempotency key to assert `isReused: true`; replay an identical signed webhook to assert `isDuplicate: true`; change one signed byte to assert 401; send an old `created_at` beyond tolerance to assert 401.

1. Load home with `GET /api/mobile/home`.
2. Register with `POST /api/mobile/auth/register`, then login with `POST /api/mobile/auth/login`.
3. Store `accessToken` and `refreshToken`.
4. Register device/FCM with `POST /api/mobile/devices`.
5. Send `Authorization: Bearer {accessToken}` for profile, cart, settings, loyalty, delivery, payment, notification, and order endpoints.
6. Load account settings with `/api/mobile/settings/contact`, `/follow-us`, `/terms`, and `/privacy-policy`.
7. Check location with `POST /api/mobile/delivery/check-zone`.
8. Create/update address with a valid `deliveryZoneId` before placing delivery orders.
9. Load active payment methods with `GET /api/mobile/payment-methods`.
10. Calculate checkout with `POST /api/mobile/orders/calculate`.
11. Preview loyalty redemption with `POST /api/mobile/loyalty/calculate` when needed.
12. Create the order with `POST /api/mobile/orders`.
13. Track with `GET /api/mobile/orders/{id}/tracking` or subscribe to `/mobile-order-hub`.
14. Cancel eligible orders with `POST /api/mobile/orders/{id}/cancel`.
15. Reorder with `POST /api/mobile/orders/{id}/reorder`.
16. Load notifications with `GET /api/mobile/notifications` and mark reads with `PUT /api/mobile/notifications/{id}/read`.
17. Refresh with `POST /api/mobile/auth/refresh-token` before the access token expires.
18. Logout with `POST /api/mobile/auth/logout`.
19. Delete account with `DELETE /api/mobile/profile/account` when the customer confirms account removal.
