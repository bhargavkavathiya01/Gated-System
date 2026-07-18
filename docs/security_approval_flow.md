# Security Approval Flow — API Documentation

## Overview

Security personnel go through an approval-based flow before getting active access to a property.
The flow has 4 steps: **Register → Submit Request → Secretory Reviews → Approve/Reject**.

> **Documents (AadharCard, AppointmentLetter) must be uploaded at Step 2 (`requestproperty`), NOT at registration.**
> The builder reviews documents as part of the approval decision, so they belong on the request itself.

---

## Base URL

```
http://nandi.runasp.net
```

---

## Flow Summary

| Step | Who | API | Purpose |
|------|-----|-----|---------|
| 1 | Security Person | `POST /api/auth/register` | Create user account only — no docs, no property |
| 2 | Security Person | `POST /api/security/requestproperty` | Submit property assignment request with documents |
| 3 | Secretory | `GET /api/builder/securityrequests` | View all pending/approved/rejected requests |
| 4 | Secretory | `GET /api/builder/securityrequests/{id}` | View single request details |
| 5 | Secretory | `POST /api/builder/approvesecurityrequest` | Approve → assigns role / Reject → no access |

---

## Step 1 — Register Security Person

**`POST /api/auth/register`**

Creates a user account in the system. No role or property is assigned at this stage.
Do not upload documents here — upload them in Step 2 along with the property request.

```bash
curl --location 'http://nandi.runasp.net/api/auth/register' \
--form 'Firstname="Final"' \
--form 'Lastname="Security"' \
--form 'Email="final.security@test.com"' \
--form 'Phone="9000000091"' \
--form 'Password="Test@1234"' \
--form 'RegisterTypeId="3"'
```

**RegisterTypeId values:**
- `1` = Management
- `2` = Flat Owner
- `3` = Security

**Response:**
```json
{
  "status": true,
  "message": "User registered successfully",
  "data": { "id": 91 }
}
```

> Note the `id` — use it as `UserId` in Step 2.

---

## Step 2 — Submit Property Request

**`POST /api/security/requestproperty`**

Security person selects which property they want to work at and uploads their documents.
This creates a **Pending** request that the builder must approve before the person gets access.

**No token required** — this is a public endpoint.

```bash
curl --location 'http://nandi.runasp.net/api/security/requestproperty' \
--form 'UserId="91"' \
--form 'PropertyId="1"' \
--form 'RoleId="6"' \
--form 'AadharCard=@"C:/path/to/aadhar.pdf"' \
--form 'AppointmentLetter=@"C:/path/to/letter.pdf"'
```

**Fields:**
| Field | Required | Description |
|-------|----------|-------------|
| `UserId` | Yes | ID from Step 1 |
| `PropertyId` | Yes | Property to be assigned to |
| `RoleId` | Yes | `6` = Security |
| `AadharCard` | No | Aadhar card file (pdf/image) |
| `AppointmentLetter` | No | Appointment letter file (pdf/image) |

**Response:**
```json
{
  "status": true,
  "message": "Security request submitted successfully. Waiting for admin approval.",
  "data": { "id": 6 }
}
```

> Note the `id` — use it as `requestId` in Step 5.

---

## Step 3 — View All Security Requests (Secretory)

**`GET /api/builder/securityrequests`**

Secretory views all security requests. Can filter by status.

```bash
curl --location 'http://nandi.runasp.net/api/builder/securityrequests' \
--header 'Authorization: Bearer BUILDER_TOKEN'
```

Filter by status:
```bash
curl --location 'http://nandi.runasp.net/api/builder/securityrequests?status=Pending' \
--header 'Authorization: Bearer BUILDER_TOKEN'
```

**Query Params:**
| Param | Values | Description |
|-------|--------|-------------|
| `status` | `Pending`, `Approved`, `Rejected` | Filter by status (optional — omit for all) |

**Response:**
```json
{
  "status": true,
  "message": "Security requests fetched successfully",
  "data": [
    {
      "id": 6,
      "propertyId": 1,
      "propertyName": "Adani Pratham",
      "userId": 91,
      "userFirstName": "Final",
      "userLastName": "Security",
      "userEmail": "final.security@test.com",
      "userPhone": "9000000091",
      "roleId": 6,
      "roleName": "Security",
      "status": "Pending",
      "approvedBy": null,
      "approvedOn": null,
      "rejectionReason": null,
      "createdOn": "2026-07-12T10:00:00",
      "aadharCard": "https://s3.amazonaws.com/...",
      "appointmentLetter": "https://s3.amazonaws.com/..."
    }
  ]
}
```

---

## Step 4 — View Single Request (Secretory)

**`GET /api/builder/securityrequests/{id}`**

Secretory views full details of a specific request before deciding to approve or reject.

```bash
curl --location 'http://nandi.runasp.net/api/builder/securityrequests/6' \
--header 'Authorization: Bearer BUILDER_TOKEN'
```

**Response:** Same structure as single item in Step 3 response, wrapped in `data`.

---

## Step 5 — Approve or Reject Request (Secretory)

**`POST /api/builder/approvesecurityrequest`**

Secretory approves or rejects the pending request.

- **Approved** → security person is immediately inserted into `tbuserrolemaster` and gets active access to the property.
- **Rejected** → request is marked Rejected, no role is assigned. `RejectionReason` is required.

**Approve:**
```bash
curl --location 'http://nandi.runasp.net/api/builder/approvesecurityrequest' \
--header 'Authorization: Bearer BUILDER_TOKEN' \
--header 'Content-Type: application/json' \
--data-raw '{
  "requestId": 6,
  "action": "Approved"
}'
```

**Reject:**
```bash
curl --location 'http://nandi.runasp.net/api/builder/approvesecurityrequest' \
--header 'Authorization: Bearer BUILDER_TOKEN' \
--header 'Content-Type: application/json' \
--data-raw '{
  "requestId": 6,
  "action": "Rejected",
  "rejectionReason": "Documents are invalid"
}'
```

**Fields:**
| Field | Required | Description |
|-------|----------|-------------|
| `requestId` | Yes | Security request ID from Step 2 |
| `action` | Yes | `Approved` or `Rejected` |
| `rejectionReason` | Required if Rejected | Reason for rejection |

**Response (Approved):**
```json
{
  "status": true,
  "message": "Security request approved successfully."
}
```

**Response (Rejected):**
```json
{
  "status": true,
  "message": "Security request rejected."
}
```

---

## What Happens on Approval

```
tblsecurityrequest  →  status = 'Approved', approvedon = NOW()
tbuserrolemaster    →  new row inserted (propertyid, userid, roleid=6, createdby=secretory)
```

Security person now has active access to the property and can log in with their role.

---

## Notes

- Existing direct assignment (`POST /api/builder/createsecretaryorsecurity`) still works and is unchanged.
- APIs are under `/api/builder/` route but are intended to be called by the **Secretory** role.
- `RoleId=6` is always Security. Do not use `3` (Secretory) or other role IDs.
- Documents uploaded in `requestproperty` are stored in S3 and also synced to the user's profile in `tblusermaster` using COALESCE (existing docs are never overwritten).
