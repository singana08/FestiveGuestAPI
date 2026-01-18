# UI Implementation Prompt for Subscription Feature

## Overview
Build subscription management UI for FestiveGuest app with user upgrade flow and admin panel.

---

## 1. USER SUBSCRIPTION FLOW

### A. Login/Register Response Changes
**Backend now includes `subscriptionStatus` in user object:**
```json
{
  "success": true,
  "user": {
    "userId": "user-123",
    "name": "John Doe",
    "email": "john@example.com",
    "subscriptionStatus": "free",  // NEW FIELD: "free", "pending", or "paid"
    ...
  },
  "token": "..."
}
```

**Action Required:**
- Update user state/context to include `subscriptionStatus`
- No additional API calls needed - it's in login/register response

### B. Subscription Upgrade Button
**Where:** User profile page or navigation bar

**Display Logic:**
```javascript
if (user.subscriptionStatus === 'free') {
  // Show "Upgrade to Premium" button
} else if (user.subscriptionStatus === 'pending') {
  // Show "Payment Under Review" badge (yellow/orange)
} else if (user.subscriptionStatus === 'paid') {
  // Show "Premium Member" badge (green)
}
```

**On Click:**
```javascript
function handleUpgrade() {
  // Redirect to WhatsApp with pre-filled message
  const message = encodeURIComponent(
    `Hi, I want to upgrade to Premium.\nUser ID: ${user.userId}\nName: ${user.name}\nEmail: ${user.email}`
  );
  window.open(`https://wa.me/919876543210?text=${message}`, '_blank');
  
  // Show instruction modal
  alert('Please send payment screenshot via WhatsApp. After admin approval, logout and login again to unlock premium features.');
}
```

### C. Premium Feature Gating
**Lock features based on subscription:**
```javascript
function PremiumFeature() {
  if (user.subscriptionStatus !== 'paid') {
    return (
      <div className="locked-feature">
        <p>🔒 Premium Feature</p>
        <button onClick={handleUpgrade}>Upgrade Now</button>
      </div>
    );
  }
  
  return <ActualFeatureComponent />;
}
```

### D. User Workflow Summary
1. User clicks "Upgrade to Premium"
2. Redirected to WhatsApp with pre-filled message
3. User sends payment screenshot
4. User logs out and waits
5. Admin approves payment
6. User logs back in → `subscriptionStatus: "paid"` → Premium unlocked

---

## 2. ADMIN PANEL

### A. Admin Access Control
**Admin Emails (hardcoded in backend):**
- admin@festiveguest.com
- kalyanimatrimony@gmail.com

**Route Protection:**
```javascript
function AdminRoute({ children }) {
  const user = JSON.parse(localStorage.getItem('user'));
  const adminEmails = ['admin@festiveguest.com', 'kalyanimatrimony@gmail.com'];
  
  if (!user || !adminEmails.includes(user.email.toLowerCase())) {
    return <Navigate to="/" />;
  }
  
  return children;
}
```

**Show Admin Link Only to Admins:**
```javascript
function Navigation() {
  const user = JSON.parse(localStorage.getItem('user'));
  const isAdmin = ['admin@festiveguest.com', 'kalyanimatrimony@gmail.com']
    .includes(user?.email?.toLowerCase());

  return (
    <nav>
      <Link to="/">Home</Link>
      {isAdmin && <Link to="/admin">Admin Panel</Link>}
    </nav>
  );
}
```

### B. Admin Dashboard Page
**Route:** `/admin`

**Features:**
1. Table showing all users with subscription status
2. Search/filter by name, email, subscription status
3. Action buttons: Approve, Pending, Revoke

**API Endpoints:**

**Fetch All Users:**
```javascript
GET /api/subscription/admin/all
Headers: { Authorization: `Bearer ${token}` }

Response:
{
  "success": true,
  "users": [
    {
      "userId": "user-123",
      "name": "John Doe",
      "email": "john@example.com",
      "phone": "9876543210",
      "userType": "Host",
      "status": "Active",
      "createdDate": "2024-01-10T08:30:00Z",
      "subscriptionStatus": "paid",
      "paymentVerifiedTimestamp": "2024-01-15T10:30:00Z",
      "lastUpdated": "2024-01-15T10:35:00Z"
    }
  ]
}
```

**Update Subscription:**
```javascript
POST /api/subscription/update
Headers: { 
  Authorization: `Bearer ${token}`,
  Content-Type: 'application/json'
}
Body: {
  "userId": "user-123",
  "subscriptionStatus": "paid",  // "free", "pending", or "paid"
  "paymentVerifiedTimestamp": "2024-01-15T10:30:00Z"  // null for free/pending
}

Response:
{
  "success": true,
  "message": "Subscription updated successfully"
}
```

### C. Admin Dashboard Component Structure

```
AdminDashboard/
├── Header (title, logout)
├── SearchBar (filter by name/email)
├── FilterTabs (All, Free, Pending, Paid)
├── UsersTable
│   ├── Columns: Name, Email, Phone, Type, Subscription, Actions
│   └── Rows: User data with action buttons
└── Pagination (if needed)
```

**Table Row Actions:**
- **Approve Button** → Sets status to "paid" with current timestamp
- **Pending Button** → Sets status to "pending" with null timestamp
- **Revoke Button** → Sets status to "free" with null timestamp

**Visual Indicators:**
- Free: Gray badge
- Pending: Yellow/Orange badge
- Paid: Green badge

### D. Admin Workflow
1. Admin logs in with admin email
2. Sees "Admin Panel" link in navigation
3. Clicks → Views all users in table
4. User sends payment screenshot via WhatsApp
5. Admin verifies payment manually
6. Admin clicks "Approve" button for that user
7. Status changes to "paid" in database
8. User logs out and back in → Premium unlocked

---

## 3. API INTEGRATION SUMMARY

### User Endpoints (No Auth Required)
```
GET /api/subscription/:userId
- Returns subscription status for a user
- Use only if you need to refresh status without re-login
```

### Admin Endpoints (Admin Auth Required)
```
GET /api/subscription/admin/all
- Returns all users with subscription status
- Use for admin dashboard table

POST /api/subscription/update
- Updates user subscription status
- Use for approve/reject actions
```

---

## 4. UI COMPONENTS TO BUILD

### Component 1: SubscriptionBadge
```jsx
<SubscriptionBadge status={user.subscriptionStatus} />
// Displays: "Free", "Pending", or "Premium" with color coding
```

### Component 2: UpgradeButton
```jsx
<UpgradeButton user={user} />
// Shows upgrade button or status badge based on subscription
```

### Component 3: PremiumFeatureGate
```jsx
<PremiumFeatureGate user={user}>
  <PremiumContent />
</PremiumFeatureGate>
// Locks content if not paid, shows upgrade prompt
```

### Component 4: AdminDashboard
```jsx
<AdminDashboard />
// Full admin panel with users table and actions
```

### Component 5: AdminRoute
```jsx
<AdminRoute><AdminDashboard /></AdminRoute>
// Protects admin routes from non-admin access
```

---

## 5. STYLING GUIDELINES

### Subscription Badges
- **Free:** Gray background (#e0e0e0), dark gray text (#666)
- **Pending:** Yellow background (#fff3cd), dark yellow text (#856404)
- **Paid/Premium:** Green background (#d4edda), dark green text (#155724)

### Upgrade Button
- Primary color, prominent placement
- Clear CTA: "Upgrade to Premium" or "Unlock Premium Features"

### Admin Table
- Clean, minimal design
- Sortable columns
- Responsive on mobile
- Action buttons clearly visible

---

## 6. ERROR HANDLING

### User Side
- If WhatsApp redirect fails, show manual contact info
- If login fails after payment, show "Contact admin" message

### Admin Side
- Show error toast if update fails
- Confirm before approving/revoking subscriptions
- Display loading state during API calls

---

## 7. TESTING CHECKLIST

### User Flow
- [ ] Login shows subscriptionStatus in user object
- [ ] Upgrade button redirects to WhatsApp
- [ ] Premium features are locked for free users
- [ ] After admin approval + re-login, premium unlocks

### Admin Flow
- [ ] Admin link only visible to admin emails
- [ ] Non-admins cannot access /admin route
- [ ] Admin can see all users with subscription status
- [ ] Approve button updates status to "paid"
- [ ] User list refreshes after update

---

## 8. OPTIONAL ENHANCEMENTS

1. **Email Notifications:** Send email when subscription approved
2. **Payment History:** Show past subscription changes
3. **Bulk Actions:** Approve multiple users at once
4. **Export Data:** Download user list as CSV
5. **Analytics:** Show subscription stats (total paid, pending, etc.)

---

## IMPLEMENTATION PRIORITY

**Phase 1 (MVP):**
1. Update user state to include subscriptionStatus
2. Add upgrade button with WhatsApp redirect
3. Lock premium features for free users
4. Build basic admin dashboard with approve/reject

**Phase 2 (Enhancement):**
1. Add search/filter in admin panel
2. Add subscription badges throughout UI
3. Improve error handling and loading states
4. Add confirmation dialogs for admin actions

**Phase 3 (Polish):**
1. Add email notifications
2. Add payment history
3. Add analytics dashboard
4. Mobile responsive improvements

---

## QUICK START CODE SNIPPETS

### Check if User is Admin
```javascript
const isAdmin = ['admin@festiveguest.com', 'kalyanimatrimony@gmail.com']
  .includes(user?.email?.toLowerCase());
```

### Upgrade to Premium
```javascript
const whatsappNumber = '919876543210'; // Replace with actual number
const message = `Hi, I want to upgrade to Premium.\nUser ID: ${user.userId}`;
window.open(`https://wa.me/${whatsappNumber}?text=${encodeURIComponent(message)}`);
```

### Fetch All Users (Admin)
```javascript
const response = await fetch('/api/subscription/admin/all', {
  headers: { 'Authorization': `Bearer ${token}` }
});
const data = await response.json();
```

### Update Subscription (Admin)
```javascript
await fetch('/api/subscription/update', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    userId: 'user-123',
    subscriptionStatus: 'paid',
    paymentVerifiedTimestamp: new Date().toISOString()
  })
});
```

---

## NOTES
- Backend is fully ready and deployed
- No database setup needed (auto-created)
- Admin emails are hardcoded in backend
- Users must logout and login after payment approval
- All subscription changes are logged with admin email for audit trail
