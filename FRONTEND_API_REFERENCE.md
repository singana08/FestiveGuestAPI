# Frontend API Reference - Complete Endpoint List

## Base URL
```
Production: https://festive-guest-api.azurewebsites.net/api
Local: http://localhost:7219/api
```

---

## 🔓 PUBLIC ENDPOINTS (No Auth Required)

### 1. Register User
```http
POST /auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123",
  "name": "John Doe",
  "phone": "9876543210",
  "userType": "Host",  // "Host" or "Guest"
  "location": "Mumbai, India",
  "bio": "I love hosting guests",
  "referredBy": "REFER123",  // Optional
  "hostingAreas": "[{\"state\":\"Maharashtra\",\"cities\":[\"Mumbai\",\"Pune\"]}]"  // Optional, JSON string
}

Response:
{
  "success": true,
  "message": "User registered successfully.",
  "user": {
    "userId": "user-123",
    "name": "John Doe",
    "email": "user@example.com",
    "userType": "Host",
    "subscriptionStatus": "free",
    "successfulReferrals": 0,
    "referralCode": "JOHN123",
    ...
  },
  "token": "eyJhbGc..."
}
```

### 2. Login
```http
POST /auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123"
}

Response:
{
  "success": true,
  "message": "Login successful.",
  "user": {
    "userId": "user-123",
    "name": "John Doe",
    "email": "user@example.com",
    "userType": "Host",  // "Host", "Guest", or "Admin"
    "subscriptionStatus": "paid",
    "successfulReferrals": 5,
    "referralCode": "JOHN123",
    ...
  },
  "token": "eyJhbGc..."
}
```

### 3. Request Password Reset OTP
```http
POST /email/request-otp
Content-Type: application/json

{
  "email": "user@example.com"
}

Response:
{
  "success": true,
  "message": "OTP sent to email"
}
```

### 4. Reset Password
```http
POST /auth/reset-password
Content-Type: application/json

{
  "email": "user@example.com",
  "otpCode": "123456",
  "newPassword": "NewPassword123"
}

Response:
{
  "success": true,
  "message": "Password reset successfully."
}
```

### 5. Get Subscription Status
```http
GET /subscription/{userId}

Response:
{
  "success": true,
  "message": "Subscription retrieved successfully",
  "subscription": {
    "userId": "user-123",
    "subscriptionStatus": "paid",  // "free", "pending", "paid"
    "paymentVerifiedTimestamp": "2024-01-15T10:30:00Z",
    "lastUpdated": "2024-01-15T10:35:00Z"
  }
}
```

### 6. Get All Locations (States with Cities)
```http
GET /location/states-with-cities

Response:
{
  "Maharashtra": ["Mumbai", "Pune", "Nagpur"],
  "Karnataka": ["Bangalore", "Mysore"],
  "Delhi": ["New Delhi"]
}
```

---

## 🔒 USER ENDPOINTS (Requires Bearer Token)

**All requests must include:**
```http
Authorization: Bearer <token>
```

### 6. Get User Profile
```http
GET /user/profile
Authorization: Bearer <token>

Response:
{
  "userId": "user-123",
  "name": "John Doe",
  "email": "jo***e@example.com",  // Masked
  "phone": "98****10",  // Masked
  "userType": "Host",
  "status": "Active",
  "location": "Mumbai, India",
  "bio": "I love hosting",
  "profileImageUrl": "https://...",
  "referralCode": "JOHN123",
  "successfulReferrals": 5,
  "hostingAreas": [
    {
      "state": "Maharashtra",
      "cities": ["Mumbai", "Pune"]
    }
  ]
}
```

### 7. Update User Profile
```http
PUT /user/profile
Authorization: Bearer <token>
Content-Type: application/json

{
  "bio": "Updated bio text",
  "hostingAreas": "[{\"state\":\"Maharashtra\",\"cities\":[\"Mumbai\"]}]"
}

Response:
{
  "success": true,
  "message": "User updated successfully.",
  "user": { ... }
}
```

### 8. Change Password
```http
POST /auth/change-password
Authorization: Bearer <token>
Content-Type: application/json

{
  "currentPassword": "OldPassword123",
  "newPassword": "NewPassword123"
}

Response:
{
  "success": true,
  "message": "Password changed successfully."
}
```

### 9. Browse Users (Guests see Hosts, Hosts see Guests)
```http
GET /user/browse
Authorization: Bearer <token>

Response:
[
  {
    "userId": "user-456",
    "name": "Jane Smith",
    "email": "ja***h@example.com",
    "phone": "98****11",
    "userType": "Guest",
    "location": "Delhi, India",
    "bio": "Looking for hosts",
    "profileImageUrl": "https://...",
    ...
  }
]
```

### 10. Get Public Profile
```http
POST /user/public-profile
Authorization: Bearer <token>
Content-Type: application/json

{
  "userId": "user-456"
}

Response:
{
  "userId": "user-456",
  "name": "Jane Smith",
  "email": "jane@example.com",
  "userType": "Guest",
  "location": "Delhi",
  "bio": "Looking for hosts",
  "profileImageUrl": "https://...",
  "hostingAreas": [...],
  "averageRating": 4.5,
  "totalReviews": 10,
  "reviews": [...]
}
```

### 11. Upload Profile Image
```http
POST /user/upload-profile-image
Authorization: Bearer <token>
Content-Type: multipart/form-data

FormData:
  file: <image-file>

Response:
{
  "imageUrl": "https://...",
  "message": "Profile image uploaded successfully"
}
```

### 12. Get Referral Stats
```http
GET /referral/stats
Authorization: Bearer <token>

Response:
{
  "referralCode": "JOHN123",
  "totalReferrals": 10,
  "successfulReferrals": 5,
  "referredUsers": [
    {
      "name": "User 1",
      "email": "user1@example.com",
      "registeredDate": "2024-01-10T08:30:00Z"
    }
  ]
}
```

---

## 👑 ADMIN ENDPOINTS (Requires Bearer Token + UserType = "Admin")

**All requests must include:**
```http
Authorization: Bearer <token>
```
**AND user must have `UserType = "Admin"` in database**

### 13. Get All Users
```http
GET /admin/users
Authorization: Bearer <token>

Response:
[
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
```

### 14. Get All Payments
```http
GET /admin/listpayments
Authorization: Bearer <token>

Response:
[
  {
    "id": "payment-123",
    "userId": "user-123",
    "upiReference": "UPI123456",
    "amount": 500,
    "status": "Verified",
    "submittedDate": "2024-01-15T10:00:00Z",
    "verifiedDate": "2024-01-15T10:30:00Z",
    "adminNotes": "Payment verified"
  }
]
```

### 15. Update Subscription (Admin Only)
```http
POST /subscription/update
Authorization: Bearer <token>
Content-Type: application/json

{
  "userId": "user-123",
  "subscriptionStatus": "paid",  // "free", "pending", "paid"
  "paymentVerifiedTimestamp": "2024-01-15T10:30:00Z"  // null for free/pending
}

Response:
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

### 16. Add Location (Admin Only)
```http
POST /location/add
Authorization: Bearer <token>
Content-Type: application/json

{
  "state": "Maharashtra",
  "city": "Mumbai"
}

Response:
{
  "success": true,
  "message": "Location added successfully"
}
```

### 17. Delete Location (Admin Only)
```http
DELETE /location/delete?state=Maharashtra&city=Mumbai
Authorization: Bearer <token>

Response:
{
  "success": true,
  "message": "Location deleted successfully"
}
```
```

---

## 📝 FRONTEND IMPLEMENTATION EXAMPLES

### Setup Axios Instance
```javascript
import axios from 'axios';

const api = axios.create({
  baseURL: 'https://festive-guest-api.azurewebsites.net/api',
  headers: {
    'Content-Type': 'application/json'
  }
});

// Add token to all requests
api.interceptors.request.use(config => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default api;
```

### Login Example
```javascript
const login = async (email, password) => {
  try {
    const response = await api.post('/auth/login', { email, password });
    
    if (response.data.success) {
      // Store token and user
      localStorage.setItem('token', response.data.token);
      localStorage.setItem('user', JSON.stringify(response.data.user));
      
      // Check if admin
      const isAdmin = response.data.user.userType === 'Admin';
      
      return { success: true, user: response.data.user, isAdmin };
    }
  } catch (error) {
    return { success: false, message: error.response?.data?.message };
  }
};
```

### Get User Profile Example
```javascript
const getUserProfile = async () => {
  try {
    const response = await api.get('/user/profile');
    return response.data;
  } catch (error) {
    console.error('Failed to fetch profile:', error);
  }
};
```

### Admin: Get All Users Example
```javascript
const getAllUsers = async () => {
  try {
    const response = await api.get('/admin/users');
    return response.data;  // Array of users
  } catch (error) {
    if (error.response?.status === 401) {
      console.error('Admin access required');
    }
  }
};
```

### Admin: Update Subscription Example
```javascript
const updateSubscription = async (userId, status) => {
  try {
    const response = await api.post('/subscription/update', {
      userId,
      subscriptionStatus: status,
      paymentVerifiedTimestamp: status === 'paid' ? new Date().toISOString() : null
    });
    
    return response.data;
  } catch (error) {
    console.error('Failed to update subscription:', error);
  }
};
```

### Check if User is Admin
```javascript
const user = JSON.parse(localStorage.getItem('user'));
const isAdmin = user?.userType === 'Admin';

// Show admin link
{isAdmin && <Link to="/admin">Admin Panel</Link>}
```

---

## 🔑 AUTHENTICATION FLOW

1. **Register/Login** → Get JWT token
2. **Store token** in localStorage
3. **Include token** in all API requests: `Authorization: Bearer <token>`
4. **Check userType** for admin access: `user.userType === 'Admin'`
5. **Logout** → Clear token and user from localStorage

---

## ⚠️ ERROR RESPONSES

All endpoints return errors in this format:
```json
{
  "success": false,
  "message": "Error description"
}
```

Common HTTP Status Codes:
- `200` - Success
- `400` - Bad Request (validation error)
- `401` - Unauthorized (missing/invalid token or not admin)
- `404` - Not Found
- `500` - Internal Server Error

---

## 📌 IMPORTANT NOTES

1. **Admin Access**: User must have `UserType = "Admin"` in database (update via Azure Portal)
2. **Token Expiry**: JWT tokens expire after 30 days
3. **Subscription Status**: "free", "pending", "paid"
4. **User Types**: "Guest", "Host", "Admin"
5. **After Admin Updates**: User must logout and login to see changes
