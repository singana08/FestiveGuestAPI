# Admin Panel Integration Guide

## Backend Endpoints for Admin Panel

### 1. GET /api/subscription/admin/all
**Purpose:** List all users with their subscription status  
**Access:** Admin only  
**Use Case:** Display users table in admin dashboard

**Request:**
```http
GET /api/subscription/admin/all
Authorization: Bearer <admin-jwt-token>
```

**Response:**
```json
{
  "success": true,
  "message": "Users retrieved successfully",
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
    },
    {
      "userId": "user-456",
      "name": "Jane Smith",
      "email": "jane@example.com",
      "phone": "9876543211",
      "userType": "Guest",
      "status": "Active",
      "createdDate": "2024-01-12T09:00:00Z",
      "subscriptionStatus": "free",
      "paymentVerifiedTimestamp": null,
      "lastUpdated": null
    }
  ]
}
```

### 2. POST /api/subscription/update
**Purpose:** Update user subscription status  
**Access:** Admin only  
**Use Case:** Approve/reject payments

**Request:**
```http
POST /api/subscription/update
Authorization: Bearer <admin-jwt-token>
Content-Type: application/json

{
  "userId": "user-123",
  "subscriptionStatus": "paid",
  "paymentVerifiedTimestamp": "2024-01-15T10:30:00Z"
}
```

**Response:**
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

---

## Frontend Admin Panel (React Example)

### Admin Dashboard Component

```jsx
import { useState, useEffect } from 'react';

function AdminDashboard() {
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const token = localStorage.getItem('token');

  useEffect(() => {
    fetchUsers();
  }, []);

  const fetchUsers = async () => {
    try {
      const response = await fetch('/api/subscription/admin/all', {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await response.json();
      if (data.success) {
        setUsers(data.users);
      }
    } catch (error) {
      console.error('Error fetching users:', error);
    } finally {
      setLoading(false);
    }
  };

  const updateSubscription = async (userId, status) => {
    try {
      const response = await fetch('/api/subscription/update', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          userId,
          subscriptionStatus: status,
          paymentVerifiedTimestamp: status === 'paid' ? new Date().toISOString() : null
        })
      });
      
      const data = await response.json();
      if (data.success) {
        alert('Subscription updated successfully!');
        fetchUsers(); // Refresh list
      } else {
        alert('Error: ' + data.message);
      }
    } catch (error) {
      console.error('Error updating subscription:', error);
      alert('Failed to update subscription');
    }
  };

  if (loading) return <div>Loading...</div>;

  return (
    <div className="admin-dashboard">
      <h1>User Management</h1>
      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Email</th>
            <th>Phone</th>
            <th>Type</th>
            <th>Subscription</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {users.map(user => (
            <tr key={user.userId}>
              <td>{user.name}</td>
              <td>{user.email}</td>
              <td>{user.phone}</td>
              <td>{user.userType}</td>
              <td>
                <span className={`badge ${user.subscriptionStatus}`}>
                  {user.subscriptionStatus}
                </span>
              </td>
              <td>
                <button onClick={() => updateSubscription(user.userId, 'paid')}>
                  Approve
                </button>
                <button onClick={() => updateSubscription(user.userId, 'pending')}>
                  Pending
                </button>
                <button onClick={() => updateSubscription(user.userId, 'free')}>
                  Revoke
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default AdminDashboard;
```

### Basic CSS

```css
.admin-dashboard {
  padding: 20px;
}

table {
  width: 100%;
  border-collapse: collapse;
}

th, td {
  padding: 12px;
  text-align: left;
  border-bottom: 1px solid #ddd;
}

.badge {
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: bold;
}

.badge.free {
  background: #e0e0e0;
  color: #666;
}

.badge.pending {
  background: #fff3cd;
  color: #856404;
}

.badge.paid {
  background: #d4edda;
  color: #155724;
}

button {
  margin-right: 5px;
  padding: 6px 12px;
  cursor: pointer;
}
```

---

## Admin Access Control

### Frontend Route Protection

```jsx
import { Navigate } from 'react-router-dom';

function AdminRoute({ children }) {
  const user = JSON.parse(localStorage.getItem('user'));
  const adminEmails = ['admin@festiveguest.com', 'kalyanimatrimony@gmail.com'];
  
  if (!user || !adminEmails.includes(user.email.toLowerCase())) {
    return <Navigate to="/" />;
  }
  
  return children;
}

// Usage in routes
<Route path="/admin" element={
  <AdminRoute>
    <AdminDashboard />
  </AdminRoute>
} />
```

### Show Admin Link Only to Admins

```jsx
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

---

## Features to Add (Optional)

### 1. Search/Filter Users
```jsx
const [search, setSearch] = useState('');
const filteredUsers = users.filter(u => 
  u.name.toLowerCase().includes(search.toLowerCase()) ||
  u.email.toLowerCase().includes(search.toLowerCase())
);
```

### 2. Filter by Subscription Status
```jsx
const [filter, setFilter] = useState('all');
const filteredUsers = users.filter(u => 
  filter === 'all' || u.subscriptionStatus === filter
);
```

### 3. Sort by Date
```jsx
const sortedUsers = [...users].sort((a, b) => 
  new Date(b.createdDate) - new Date(a.createdDate)
);
```

---

## Summary

**Backend provides:**
- ✅ GET /api/subscription/admin/all - List all users
- ✅ POST /api/subscription/update - Update subscription
- ✅ Admin-only access via JWT token

**Frontend needs:**
- ✅ Admin dashboard page
- ✅ Users table with subscription status
- ✅ Approve/Reject buttons
- ✅ Route protection for admin-only access

**Admin emails (hardcoded in backend):**
- admin@festiveguest.com
- kalyanimatrimony@gmail.com
