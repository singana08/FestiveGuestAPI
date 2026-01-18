# Subscription Integration Guide

## ✅ Subscription Status in Login/Register Response

**No additional API calls needed!** Subscription status is automatically included in login and register responses.

### Login Response Example:
```json
{
  "success": true,
  "message": "Login successful.",
  "user": {
    "userId": "user-123",
    "name": "John Doe",
    "email": "john@example.com",
    "subscriptionStatus": "paid",  // ← Included automatically
    "userType": "Host",
    ...
  },
  "token": "eyJhbGc..."
}
```

### Register Response Example:
```json
{
  "success": true,
  "message": "User registered successfully.",
  "user": {
    "userId": "user-456",
    "name": "Jane Smith",
    "email": "jane@example.com",
    "subscriptionStatus": "free",  // ← Defaults to "free" for new users
    "userType": "Guest",
    ...
  },
  "token": "eyJhbGc..."
}
```

## Frontend Usage

### Check Subscription on Login
```javascript
const loginResponse = await fetch('/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, password })
});

const data = await loginResponse.json();

// Access subscription status directly
if (data.user.subscriptionStatus === 'paid') {
  // Unlock premium features
  enablePremiumFeatures();
} else if (data.user.subscriptionStatus === 'pending') {
  // Show "Payment under review" message
  showPendingMessage();
} else {
  // Show upgrade prompt
  showUpgradePrompt();
}
```

### Store in State/Context
```javascript
// React example
const [user, setUser] = useState(null);

useEffect(() => {
  if (data.success) {
    setUser(data.user); // Includes subscriptionStatus
    localStorage.setItem('token', data.token);
  }
}, []);

// Access anywhere
const isPremium = user?.subscriptionStatus === 'paid';
```

## User Flow After Payment

### Simple Approach (Recommended)
1. User clicks "Upgrade" → Redirected to WhatsApp
2. User sends payment screenshot
3. **User logs out and waits for admin approval**
4. Admin verifies payment → Updates status to "paid"
5. **User logs back in → Premium features unlocked automatically**

### Frontend Implementation
```javascript
// After user sends payment screenshot
function handlePaymentSent() {
  alert('Payment submitted! Please logout and login again after admin approval.');
  // Optionally logout immediately
  logout();
}
```

## When to Use GET /api/subscription/:userId

Only use the separate endpoint for:
- ✅ **Admin panel** - View user subscriptions
- ✅ **Manual refresh** - Check status without re-login (optional)

**You don't need it for normal user flow!** Login response is enough.

## Summary

| Scenario | Method | Reason |
|----------|--------|--------|
| **Login** | Use login response | Subscription included automatically |
| **Register** | Use register response | Defaults to "free" |
| **After payment** | Logout → Login | Fresh subscription status on login |
| **Admin panel** | GET /api/subscription/:userId | View user subscriptions |
| **Admin updates** | POST /api/subscription/update | Admin only |

**Recommendation:** Just use login/register response. Ask users to logout and login after payment approval.
