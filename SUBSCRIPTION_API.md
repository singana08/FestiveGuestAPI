# Subscription API Documentation

## Overview
The Subscription API manages user subscription status for FestiveGuest, supporting free, pending, and paid tiers.

## Database Schema

### Subscriptions Table
- **PartitionKey**: "Subscription" (constant)
- **RowKey**: userId (unique identifier)
- **UserId**: User identifier (string)
- **SubscriptionStatus**: Enum - "free", "pending", "paid"
- **PaymentVerifiedTimestamp**: DateTime when payment was verified (nullable)
- **UpdatedByAdmin**: Email of admin who made the update (audit trail)
- **Timestamp**: Auto-generated last update timestamp

## Endpoints

### 1. GET /api/subscription/:userId
Retrieves subscription status for a user.

**Access**: Public (no authentication required)

**Response**:
```json
{
  "success": true,
  "message": "Subscription retrieved successfully",
  "subscription": {
    "userId": "user-123",
    "subscriptionStatus": "paid",
    "paymentVerifiedTimestamp": "2024-01-15T10:30:00Z",
    "lastUpdated": "2024-01-15T10:35:00Z"
  }
}
```

**Default Response** (no subscription found):
```json
{
  "success": true,
  "message": "No subscription found",
  "subscription": {
    "userId": "user-123",
    "subscriptionStatus": "free",
    "paymentVerifiedTimestamp": null,
    "lastUpdated": null
  }
}
```

### 2. POST /api/subscription/update
Updates user subscription status (Admin only).

**Access**: Admin only (requires JWT token with admin email)

**Admin Emails**:
- admin@festiveguest.com
- kalyanimatrimony@gmail.com

**Headers**:
```
Authorization: Bearer <jwt-token>
Content-Type: application/json
```

**Request Body**:
```json
{
  "userId": "user-123",
  "subscriptionStatus": "paid",
  "paymentVerifiedTimestamp": "2024-01-15T10:30:00Z"
}
```

**Validation**:
- `userId`: Required
- `subscriptionStatus`: Required, must be "free", "pending", or "paid"
- `paymentVerifiedTimestamp`: Optional, DateTime

**Response**:
```json
{
  "success": true,
  "message": "Subscription updated successfully",
  "subscription": {
    "userId": "user-123",
    "subscriptionStatus": "paid",
    "paymentVerifiedTimestamp": "2024-01-15T10:30:00Z",
    "lastUpdated": "2024-01-15T10:35:00Z"
  }
}
```

**Error Response** (Non-admin):
```json
{
  "success": false,
  "message": "Admin access required"
}
```

## Subscription Flow

### User Journey
1. **User clicks "Upgrade"** → Frontend redirects to WhatsApp
2. **User sends payment screenshot** via WhatsApp
3. **Admin verifies payment** manually
4. **Admin calls POST /api/subscription/update** with:
   - `subscriptionStatus: "paid"`
   - `paymentVerifiedTimestamp: <current-datetime>`
5. **Frontend polls GET /api/subscription/:userId** to check status
6. **Features unlock** when status changes to "paid"

### Status Transitions
- **free** → **pending**: User initiated upgrade, awaiting payment
- **pending** → **paid**: Admin verified payment
- **paid** → **free**: Subscription expired or revoked
- **pending** → **free**: Payment rejected

## Security

### Admin Authorization
- Implemented via `AdminAuthorizeAttribute`
- Checks JWT token for email claim
- Only whitelisted admin emails can update subscriptions
- All updates are logged with admin email for audit trail

### Logging
All subscription updates are logged with:
- User ID
- New subscription status
- Admin email who made the change
- Timestamp

Example log:
```
Subscription updated for user user-123 to paid by admin admin@festiveguest.com
```

## Testing

Use the provided `test-subscription-endpoints.http` file:

1. Replace `{{userId}}` with actual user ID
2. Replace `{{adminToken}}` with valid admin JWT token
3. Run requests in VS Code with REST Client extension

## Integration Notes

### Frontend Integration
```javascript
// Check subscription status
const response = await fetch(`/api/subscription/${userId}`);
const data = await response.json();

if (data.subscription.subscriptionStatus === 'paid') {
  // Unlock premium features
}
```

### Admin Panel Integration
```javascript
// Update subscription (admin only)
const response = await fetch('/api/subscription/update', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${adminToken}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    userId: 'user-123',
    subscriptionStatus: 'paid',
    paymentVerifiedTimestamp: new Date().toISOString()
  })
});
```

## Error Handling

| Status Code | Description |
|-------------|-------------|
| 200 | Success |
| 400 | Bad request (invalid input) |
| 401 | Unauthorized (missing/invalid token) |
| 403 | Forbidden (non-admin user) |
| 404 | User not found |
| 500 | Internal server error |
