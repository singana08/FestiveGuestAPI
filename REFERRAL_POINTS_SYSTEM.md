# Referral Points System Documentation

## Overview
Users earn points for successful referrals and can redeem them for free subscription.

## Points Structure
- **1 successful referral = 100 points**
- **500 points = 3 months subscription**
- **5 referrals needed** for free subscription
- **Points never expire**

## How It Works

### 1. Earning Points
When a new user registers with a referral code:
- Referrer automatically gets **100 points**
- Points are added to their account immediately
- Transaction is logged in PointsTransactions table

### 2. Redeeming Points
User can redeem 500 points for 3 months subscription:
- **Requirement**: Must have at least 500 points
- **Restriction**: Cannot redeem if already have active paid subscription
- **Result**: 500 points deducted, 3 months subscription activated

### 3. Points Never Expire
- Points remain in account forever
- Encourages long-term engagement
- No pressure to redeem quickly

---

## API Endpoints

### 1. Redeem Points (User)
```http
POST /api/referralpoints/redeem
Authorization: Bearer <token>

Response:
{
  "success": true,
  "message": "Points redeemed successfully! 3 months subscription activated.",
  "remainingPoints": 100,
  "subscriptionStatus": "paid",
  "subscriptionExpiryDate": "2024-04-15T10:30:00Z"
}
```

**Errors:**
- Insufficient points: `"Need 500 points to redeem"`
- Active subscription: `"Cannot redeem. You already have an active paid subscription"`

### 2. Get Points History (User)
```http
GET /api/referralpoints/history
Authorization: Bearer <token>

Response:
[
  {
    "userId": "user-123",
    "points": 100,
    "type": "earned",
    "description": "Earned 100 points for referring user user-456",
    "createdDate": "2024-01-15T10:30:00Z"
  },
  {
    "userId": "user-123",
    "points": -500,
    "type": "redeemed",
    "description": "Redeemed 500 points for 3 months subscription",
    "createdDate": "2024-01-20T14:00:00Z"
  }
]
```

### 3. Adjust Points (Admin Only)
```http
POST /api/referralpoints/adjust
Authorization: Bearer <token>
Content-Type: application/json

{
  "userId": "user-123",
  "points": 200,
  "description": "Bonus points for promotion"
}

Response:
{
  "success": true,
  "message": "Points adjusted successfully",
  "newBalance": 700
}
```

**Use Cases:**
- Add bonus points: `"points": 200`
- Deduct points: `"points": -100`
- Correction: `"points": 50, "description": "Correction for duplicate referral"`

---

## User Profile Response

Login/Profile endpoints now include `referralPoints`:

```json
{
  "userId": "user-123",
  "name": "John Doe",
  "referralCode": "JOHN123",
  "successfulReferrals": 5,
  "referralPoints": 500,
  "subscriptionStatus": "free",
  ...
}
```

---

## Admin Dashboard

GET /admin/users now includes `referralPoints`:

```json
[
  {
    "userId": "user-123",
    "name": "John Doe",
    "referralCode": "JOHN123",
    "successfulReferrals": 5,
    "referralPoints": 500,
    "subscriptionStatus": "free",
    ...
  }
]
```

---

## Database Tables

### Users Table (Updated)
Added field:
- `ReferralPoints` (int, default 0)

### PointsTransactions Table (New)
Tracks all point transactions:
- `PartitionKey`: userId
- `RowKey`: transaction ID (GUID)
- `UserId`: User identifier
- `Points`: Points amount (positive for earn, negative for redeem)
- `Type`: "earned", "redeemed", "admin_adjustment"
- `Description`: Transaction description
- `CreatedDate`: When transaction occurred

---

## Frontend Integration

### Display Points Balance
```javascript
const user = JSON.parse(localStorage.getItem('user'));
console.log(`You have ${user.referralPoints} points`);
```

### Redeem Points Button
```javascript
const redeemPoints = async () => {
  if (user.referralPoints < 500) {
    alert('You need 500 points to redeem');
    return;
  }
  
  if (user.subscriptionStatus === 'paid') {
    alert('Cannot redeem while you have active subscription');
    return;
  }
  
  const response = await api.post('/referralpoints/redeem');
  if (response.data.success) {
    alert('3 months subscription activated!');
    // Refresh user data
    window.location.reload();
  }
};
```

### Show Points History
```javascript
const getHistory = async () => {
  const response = await api.get('/referralpoints/history');
  setHistory(response.data);
};
```

### Admin: Adjust Points
```javascript
const adjustPoints = async (userId, points, description) => {
  await api.post('/referralpoints/adjust', {
    userId,
    points,
    description
  });
};
```

---

## Business Rules

1. **Earning Points**
   - Only when referred user completes registration (Status = "Active")
   - Automatic - no manual intervention needed
   - 100 points per referral

2. **Redeeming Points**
   - Requires exactly 500 points (no partial redemption)
   - Cannot redeem if active paid subscription exists
   - Gives 3 months subscription (both Guest & Host)
   - Points are deducted immediately

3. **Points Expiry**
   - Points never expire
   - Remain in account forever

4. **Admin Controls**
   - Can add/deduct points manually
   - All adjustments logged with admin email
   - Useful for promotions, corrections, bonuses

---

## Example Scenarios

### Scenario 1: User Earns and Redeems
1. User refers 5 friends → Earns 500 points
2. User clicks "Redeem Points" → Gets 3 months subscription
3. Points balance: 0
4. Subscription status: "paid"

### Scenario 2: User Has Paid Subscription
1. User has 500 points
2. User already paid for subscription
3. User tries to redeem → Error: "Cannot redeem while active subscription"
4. User must wait for subscription to expire

### Scenario 3: Admin Bonus
1. Admin gives 200 bonus points for promotion
2. User now has 700 points
3. User redeems 500 → Gets 3 months subscription
4. Remaining: 200 points (saved for next redemption)

---

## Testing Checklist

- [ ] User registers with referral code → Referrer gets 100 points
- [ ] User with 500 points redeems → Gets 3 months subscription
- [ ] User with <500 points cannot redeem
- [ ] User with active paid subscription cannot redeem
- [ ] Points history shows all transactions
- [ ] Admin can add/deduct points
- [ ] Points appear in login/profile response
- [ ] Points appear in admin dashboard

---

## Notes

- Points are awarded immediately upon successful registration
- No delay or manual approval needed
- All transactions are logged for audit
- Points balance is always accurate and real-time
