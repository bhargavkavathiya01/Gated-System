# Gated System — API Documentation

**Base URL:** `http://nandi.runasp.net/api`
**Authentication:** All endpoints require `Authorization: Bearer <token>` header unless noted otherwise.
**Date:** June 2026

---

## Table of Contents

1. [Authentication](#1-authentication)
2. [User](#2-user)
3. [Builder](#3-builder)
4. [Super Admin](#4-super-admin)
5. [Flat Owner](#5-flat-owner)
6. [Secretary](#6-secretary)
7. [SOS](#7-sos)

---

## 1. Authentication

### POST `/api/auth/login`

Login and receive access token.

**Request Body (JSON):**
```json
{
  "user": "9999999999",
  "password": "yourpassword",
  "deviceToken": "fcm_token_here",
  "platform": "android"
}
```

**Response:**
```json
{
  "status": true,
  "message": "Login successful",
  "data": {
    "accessToken": "eyJ...",
    "refreshToken": "abc123",
    "accessTokenExpiresAt": "2026-06-03T10:00:00Z",
    "userData": {
      "id": 5,
      "firstname": "John",
      "middlename": "",
      "lastname": "Doe",
      "email": "john@gmail.com",
      "phone": "9999999999",
      "permanentQR": "QR_TOKEN",
      "userRegistrationTypeId": 2,
      "profilePictureUrl": "https://s3.../profile.jpg",
      "aadharCard": "https://s3.../AadharCards/uuid.jpg",
      "electricityBill": "https://s3.../ElectricityBills/uuid.jpg"
    },
    "roleData": [
      {
        "roleId": 4,
        "roleName": "Flat Owner",
        "propertyId": 1,
        "buildingId": 2,
        "flatNumber": "A-101",
        "propertyName": "Nandi Heights",
        "buildingName": "Block A"
      }
    ]
  }
}
```

**cURL:**
```bash
curl -X POST "http://localhost:5000/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "user": "9999999999",
    "password": "yourpassword",
    "deviceToken": "fcm_token",
    "platform": "android"
  }'
```

---

## 2. User

### GET `/api/user/getallusers`

Returns basic details of all registered users. Used for selecting SOS contacts or searching users.

**Response:**
```json
{
  "status": true,
  "message": "Users fetched successfully",
  "data": [
    {
      "id": 5,
      "firstname": "John",
      "middlename": "",
      "lastname": "Doe",
      "email": "john@gmail.com",
      "phone": "9999999999",
      "isActive": true,
      "roleId": 4,
      "roleName": "Flat Owner"
    }
  ]
}
```

> **Note:** A user with multiple roles may appear more than once (once per role).

**cURL:**
```bash
curl -X GET "http://localhost:5000/api/user/getallusers" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

### PUT `/api/user/update-profile`

Update the logged-in user's profile.

**Request (form-data):**

| Field | Type | Required |
|-------|------|----------|
| `Firstname` | Text | No |
| `Lastname` | Text | No |
| `Email` | Text | No |
| `Phone` | Text | No |
| `ProfileImage` | File | No |

---

### POST `/api/user/reset-password`

Reset password for the logged-in user.

**Request Body (JSON):**
```json
{
  "oldPassword": "current_password",
  "newPassword": "new_password"
}
```

---

### POST `/api/user/update-fcm-token`

Update the FCM device token for push notifications.

**Request Body (JSON):**
```json
{
  "fcmToken": "new_device_token",
  "modifiedBy": 0
}
```

---

## 3. Builder

### POST `/api/builder/createflatowner`

Submit a flat owner creation request. Requires admin approval before the flat owner is activated.

**Request (form-data):**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `PropertyId` | Text | ✅ | Property ID |
| `BuildingId` | Text | ✅ | Building ID |
| `FlatNo` | Text | ✅ | Flat number e.g. `A-101` |
| `UserId` | Text | ✅ | Existing user's ID to assign as flat owner |
| `RoleId` | Text | ✅ | Flat owner role ID |
| `GuestType` | Text | No | Guest type (leave empty for standard flat owner) |
| `AadharCard` | File | No | Aadhar card image/pdf |
| `ElectricityBill` | File | No | Electricity bill image/pdf |

**Response:**
```json
{
  "status": true,
  "message": "Flat Owner creation request submitted successfully. Waiting for admin approval.",
  "data": { "id": 12 }
}
```

**cURL:**
```bash
curl -X POST "http://localhost:5000/api/builder/createflatowner" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "PropertyId=1" \
  -F "BuildingId=2" \
  -F "FlatNo=A-101" \
  -F "UserId=5" \
  -F "RoleId=3" \
  -F "AadharCard=@/path/to/aadhar.jpg" \
  -F "ElectricityBill=@/path/to/bill.jpg"
```

---

### GET `/api/builder/flatownerrequests`

Get flat owner requests submitted by the builder.

**Query Parameters:**

| Param | Values | Description |
|-------|--------|-------------|
| `status` | `Pending` / `Approved` / `Rejected` | Filter by status (optional) |

**Response:**
```json
{
  "status": true,
  "message": "Flat owner requests fetched successfully",
  "data": [
    {
      "id": 12,
      "propertyId": 1,
      "propertyName": "Nandi Heights",
      "buildingId": 2,
      "buildingName": "Block A",
      "flatNumber": "A-101",
      "userId": 5,
      "userFirstName": "John",
      "userLastName": "Doe",
      "userEmail": "john@gmail.com",
      "userPhone": "9999999999",
      "roleId": 4,
      "roleName": "Flat Owner",
      "status": "Pending",
      "approvedBy": null,
      "approvedByName": null,
      "approvedOn": null,
      "rejectionReason": null,
      "createdOn": "2026-06-01T10:00:00Z",
      "aadharCard": "https://s3.../AadharCards/uuid.jpg",
      "electricityBill": "https://s3.../ElectricityBills/uuid.jpg"
    }
  ]
}
```

**cURL:**
```bash
curl -X GET "http://localhost:5000/api/builder/flatownerrequests?status=Pending" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## 4. Super Admin

### GET `/api/superadmin/flatownerrequests`

Get all flat owner requests (admin view).

**Query Parameters:**

| Param | Values |
|-------|--------|
| `status` | `Pending` / `Approved` / `Rejected` |

**Response fields include:** `approvedBy`, `approvedByName`, `approvedOn`, `createdOn`, `aadharCard`, `electricityBill`, `rejectionReason`

**cURL:**
```bash
curl -X GET "http://localhost:5000/api/superadmin/flatownerrequests?status=Pending" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

### POST `/api/superadmin/approveflatownerrequest`

Approve or reject a flat owner request. Records `approvedOn` datetime for both actions.

**Request Body (JSON):**
```json
{
  "requestId": 12,
  "action": "Approved",
  "rejectionReason": null
}
```

> For rejection: set `"action": "Rejected"` and provide `"rejectionReason"`.

**Response:**
```json
{
  "status": true,
  "message": "Flat owner request approved successfully"
}
```

**cURL:**
```bash
# Approve
curl -X POST "http://localhost:5000/api/superadmin/approveflatownerrequest" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "requestId": 12, "action": "Approved" }'

# Reject
curl -X POST "http://localhost:5000/api/superadmin/approveflatownerrequest" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "requestId": 12, "action": "Rejected", "rejectionReason": "Documents unclear" }'
```

---

### POST `/api/superadmin/verifyproperty`

Approve or reject a property verification request. Admin ID is taken from JWT.

**Request Body (JSON):**
```json
{
  "propertyId": 3,
  "isVerified": "Approved"
}
```

> Values for `isVerified`: `Approved` / `Rejected` / `Pending`

**cURL:**
```bash
curl -X POST "http://localhost:5000/api/superadmin/verifyproperty" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "propertyId": 3, "isVerified": "Approved" }'
```

---

### GET `/api/superadmin/getpendingproperties`

Get all properties pending verification.

**Response fields include:** `createdOn`, `modifiedOn` (verification datetime), `verifiedBy`, `verifiedByName`

**cURL:**
```bash
curl -X GET "http://localhost:5000/api/superadmin/getpendingproperties" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## 5. Flat Owner

### POST `/api/flatowner/approveorrejectmanualvisitor`

Approve or reject a manual visitor entry request from a guard.

**Request Body (JSON):**
```json
{
  "requestId": 8,
  "status": "Approved",
  "remarks": "Visitor verified"
}
```

> Values for `status`: `Approved` / `Rejected`

**Response:** Push notification sent to guard. `approvedBy` and `approvedOn` recorded.

**cURL:**
```bash
curl -X POST "http://localhost:5000/api/flatowner/approveorrejectmanualvisitor" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "requestId": 8, "status": "Approved", "remarks": "OK" }'
```

---

## 6. Secretary

### POST `/api/secretory/uploadsocietyimages`

Upload one or more society images for a property. Images are stored in AWS S3.

**Request (form-data):**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `PropertyId` | Text | ✅ | Property ID |
| `ImageTitle` | Text | No | Caption/title for the images |
| `Images` | File | ✅ | One or more image files (repeat field for multiple) |

**Response:**
```json
{
  "status": true,
  "message": "Society images uploaded successfully",
  "data": {
    "count": 2,
    "ids": [1, 2]
  }
}
```

**cURL:**
```bash
curl -X POST "http://localhost:5000/api/secretory/uploadsocietyimages" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "PropertyId=1" \
  -F "ImageTitle=Society Gate" \
  -F "Images=@/path/to/image1.jpg" \
  -F "Images=@/path/to/image2.jpg"
```

---

### GET `/api/secretory/getsocietyimages`

Get all images uploaded for a property. Visible to all society members.

**Query Parameters:**

| Param | Required | Description |
|-------|----------|-------------|
| `propertyId` | ✅ | Property ID |

**Response:**
```json
{
  "status": true,
  "message": "Society images fetched successfully",
  "data": [
    {
      "id": 1,
      "propertyId": 1,
      "imageUrl": "https://s3.../SocietyImages/uuid.jpg",
      "imageTitle": "Society Gate",
      "uploadedBy": 7,
      "uploadedByName": "John Secretary",
      "createdOn": "2026-06-02T10:00:00Z"
    }
  ]
}
```

**cURL:**
```bash
curl -X GET "http://localhost:5000/api/secretory/getsocietyimages?propertyId=1" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## 7. SOS

All SOS endpoints read the **logged-in user's ID from the JWT token** automatically.

---

### POST `/api/user/addSOScontact`

Add a user as an SOS emergency contact with an optional relation label.

**Request Body (JSON):**
```json
{
  "contactUserId": 5,
  "relation": "Father"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `contactUserId` | ✅ | User ID of the contact to add |
| `relation` | No | Relationship label e.g. `Father`, `Friend`, `Neighbor` |

**Response:**
```json
{
  "status": true,
  "message": "SOS contact added successfully",
  "data": { "id": 3 }
}
```

**cURL:**
```bash
curl -X POST "http://localhost:5000/api/user/addSOScontact" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "contactUserId": 5, "relation": "Father" }'
```

---

### GET `/api/user/getsoscontacts`

Get all SOS contacts of the logged-in user.

**Response:**
```json
{
  "status": true,
  "message": "SOS contacts fetched successfully",
  "data": [
    {
      "id": 3,
      "userId": 10,
      "contactUserId": 5,
      "contactFullName": "Jane Doe",
      "contactEmail": "jane@gmail.com",
      "contactPhone": "9876543210",
      "contactProfileImage": "https://s3.../profile.jpg",
      "relation": "Father",
      "createdOn": "2026-06-02T09:00:00Z"
    }
  ]
}
```

**cURL:**
```bash
curl -X GET "http://localhost:5000/api/user/getsoscontacts" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

### PUT `/api/user/updateSOScontact/{id}`

Update the relation label of an existing SOS contact.

**Path Parameter:** `id` — SOS contact record ID (from `getsoscontacts` response)

**Request Body (JSON):**
```json
{
  "relation": "Brother"
}
```

**Response:**
```json
{
  "status": true,
  "message": "SOS contact updated successfully"
}
```

**cURL:**
```bash
curl -X PUT "http://localhost:5000/api/user/updateSOScontact/3" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "relation": "Brother" }'
```

---

### DELETE `/api/user/removeSOScontact/{id}`

Remove a user from SOS contacts.

**Path Parameter:** `id` — SOS contact record ID

**Response:**
```json
{
  "status": true,
  "message": "SOS contact removed successfully"
}
```

**cURL:**
```bash
curl -X DELETE "http://localhost:5000/api/user/removeSOScontact/3" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

### POST `/api/user/triggersos`

Send an emergency SOS push notification to all the logged-in user's SOS contacts instantly.

**Request Body:** None

**Response:**
```json
{
  "status": true,
  "message": "SOS alert sent to 3 contact(s)."
}
```

> If no SOS contacts are set or none have the app installed, returns 400 with an appropriate message.

**Push Notification received by contacts:**
```
Title: 🚨 SOS Alert
Body:  A contact needs your help urgently!
Data:  { type: "sos_alert", senderId: "10" }
```

**cURL:**
```bash
curl -X POST "http://localhost:5000/api/user/triggersos" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## Approval Datetime Tracking Summary

All three approval flows now record and return timestamps:

| Flow | Request Submitted | Action Taken | Who Acted |
|------|------------------|--------------|-----------|
| Flat Owner Request | `createdOn` | `approvedOn` | `approvedBy` + `approvedByName` |
| Visitor Manual Entry | `createdon` | `approvedon` | `approvedby` |
| Property Verification | `createOn` | `modifiedOn` | `verifiedBy` + `verifiedByName` |

---

## DB Tables Created

| Table | Purpose |
|-------|---------|
| `tblsoscontacts` | Stores each user's SOS contact list with optional relation |
| `tblsocietyimages` | Stores society image URLs uploaded by the secretary |

## Stored Procedures Created / Modified

| SP | Change |
|----|--------|
| `sp_api_soscontacts` | New — CRUD for SOS contacts + device token fetch |
| `sp_api_societyimages` | New — insert and get society images |
| `sp_api_flatownerrequest` | Modified — stores `aadharcard`, `electricitybill`; sets `approvedon` for both Approved & Rejected |
| `sp_api_visitorrequest` | Modified — sets `approvedby`, `approvedon` on status update |
| `sp_api_getflatownerrequests` | Modified — returns `aadharcard`, `electricitybill` |
| `sp_api_getflatownerrequestbyid` | Modified — returns `aadharcard`, `electricitybill` |
| `sp_api_superadminpropertymaster` | Modified — returns `verifiedby`, `verifiedbyname` |
| `sp_api_authenticateuser` | Modified — returns `aadharCard`, `electricityBill` |
