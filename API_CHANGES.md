# API Changes

Base URL: `https://localhost:44308`

---

## 1. POST /api/auth/register

**Content-Type:** `multipart/form-data`

Added `AppointmentLetter` (optional file — PDF or image).

| Field | Type | Notes |
|---|---|---|
| Firstname | string | required |
| Middlename | string | optional |
| Lastname | string | required |
| Email | string | required |
| Phone | string | required |
| Password | string | required |
| RegisterTypeId | int | required |
| AadharCard | file | optional — all users |
| ElectricityBill | file | optional — FlatOwner only |
| AppointmentLetter | file | optional — Security only |

```bash
# Security user
curl --location 'https://localhost:44308/api/auth/register' \
  --form 'Firstname="John"' \
  --form 'Lastname="Doe"' \
  --form 'Email="john.security@example.com"' \
  --form 'Phone="9876543210"' \
  --form 'Password="Test@123"' \
  --form 'RegisterTypeId="3"' \
  --form 'AadharCard=@"/path/to/aadhar.jpg"' \
  --form 'AppointmentLetter=@"/path/to/appointment.pdf"'
```

---

## 2. POST /api/auth/login

**No change in request.**

`userData` in response now includes `appointmentLetter`:

```json
{
  "userData": {
    "id": 5,
    "firstname": "John",
    "lastname": "Doe",
    "email": "john.security@example.com",
    "phone": "9876543210",
    "aadharCard": "https://s3.../aadhar.jpg",
    "electricityBill": null,
    "appointmentLetter": "https://s3.../appointment.pdf"
  }
}
```

> FlatOwner → `aadharCard` + `electricityBill` set, `appointmentLetter` null  
> Security → `aadharCard` + `appointmentLetter` set, `electricityBill` null

```bash
curl --location 'https://localhost:44308/api/auth/login' \
  --header 'Content-Type: application/json' \
  --data '{
    "user": "john.security@example.com",
    "password": "Test@123"
  }'
```

---

## 3. POST /api/builder/createsecretaryorsecurity

**Changed from JSON to `multipart/form-data`.**  
Added `AadharCard` and `AppointmentLetter` file fields.

| Field | Type | Notes |
|---|---|---|
| PropertyId | int | required |
| UserId | int | required |
| RoleId | int | required |
| AadharCard | file | optional |
| AppointmentLetter | file | optional |

```bash
curl --location 'https://localhost:44308/api/builder/createsecretaryorsecurity' \
  --header 'Authorization: Bearer <token>' \
  --form 'PropertyId="1"' \
  --form 'UserId="5"' \
  --form 'RoleId="3"' \
  --form 'AadharCard=@"/path/to/aadhar.jpg"' \
  --form 'AppointmentLetter=@"/path/to/appointment.pdf"'
```

---

## 4. GET /api/builder/propertiesbybuilder

**No change in request.**

Each property now includes a `buildings` array:

```json
{
  "data": [
    {
      "id": 1,
      "propertyName": "Green Apartments",
      "address": "123 Main St",
      "city": "Ahmedabad",
      "isVerified": "Approved",
      "buildings": [
        { "buildingId": 1, "buildingName": "A" },
        { "buildingId": 2, "buildingName": "B" }
      ]
    }
  ]
}
```

```bash
curl --location 'https://localhost:44308/api/builder/propertiesbybuilder' \
  --header 'Authorization: Bearer <token>'
```

---

## 5. GET /api/superadmin/getallproperties

**No change in request.**

Each property now includes a `buildings` array (same shape as above).

```bash
curl --location 'https://localhost:44308/api/superadmin/getallproperties' \
  --header 'Authorization: Bearer <token>'
```
