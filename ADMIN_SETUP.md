# Admin Setup Guide

## How to Make a User Admin

Admin access is controlled by the `UserType` field in the database. No hardcoded emails.

### Step 1: Register a User
Register normally via API or frontend:
```json
POST /api/auth/register
{
  "email": "admin@festiveguest.com",
  "password": "Admin@123",
  "name": "Admin",
  "phone": "9999999999",
  "userType": "Host",  // Will be changed to "Admin" in database
  "location": "India"
}
```

### Step 2: Update UserType in Database
Go to Azure Portal → Table Storage → Users table:
1. Find the user by email
2. Edit the row
3. Change `UserType` from "Host" or "Guest" to **"Admin"**
4. Save

### Step 3: User Logs Out and Logs In
- User must logout and login again
- New JWT token will include `userType: "Admin"`
- Admin panel will be accessible

---

## How It Works

### Backend
- JWT token includes `userType` claim
- `AdminAuthorizeAttribute` checks if `userType == "Admin"`
- No hardcoded emails - purely database-driven

### Frontend
Check if user is admin:
```javascript
const user = JSON.parse(localStorage.getItem('user'));
const isAdmin = user?.userType?.toLowerCase() === 'admin';

// Show admin link
{isAdmin && <Link to="/admin">Admin Panel</Link>}
```

### Admin Route Protection
```javascript
function AdminRoute({ children }) {
  const user = JSON.parse(localStorage.getItem('user'));
  
  if (!user || user.userType?.toLowerCase() !== 'admin') {
    return <Navigate to="/" />;
  }
  
  return children;
}
```

---

## UserType Values

| Value | Description |
|-------|-------------|
| **Guest** | Regular guest user |
| **Host** | Regular host user |
| **Admin** | Admin user (full access) |

---

## To Create Multiple Admins

1. Register users normally
2. Update their `UserType` to "Admin" in database
3. They logout and login
4. Admin access granted

---

## API Response

Login response includes `userType`:
```json
{
  "success": true,
  "user": {
    "userId": "user-123",
    "name": "Admin",
    "email": "admin@festiveguest.com",
    "userType": "Admin",  // ← Check this field
    ...
  },
  "token": "..."
}
```

---

## Summary

✅ No hardcoded emails  
✅ Database-driven admin access  
✅ Change `UserType` to "Admin" in database  
✅ User must logout/login after change  
✅ Frontend checks `user.userType === "Admin"`
