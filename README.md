# API_App — REST API Documentation

## Overview
ASP.NET Core 8.0 minimal API
Database: PostgreSQL
Communication format: JSON
Authentication: JWT Bearer

---

## Base URL
https://localhost:7001


---

## Endpoints

### 1. `POST /login`
Authenticate a user.

**Request Body:**
```json
{
    "username": "User1",
    "password": "Pass1"
}
```

**Response Body:**
```json
{
    "access_token": <token>
}
```


### 2. `POST /logout`

**Request Headers:**
```json
Authorization:	Bearer <token>
```

**Response Body:**
```json
{
    "message": "Logged out"
}
```


### 3. `POST /payment`
Authenticate a user.

**Request Headers:**
```json
Authorization:	Bearer <token>
```

**Response Body:**
```json
{
    "message": "Payment successful",
    "balance_usd": "6.90"
}
```


### 4. `POST /create-user` (debug only)
Authenticate a user.

**Request Headers:**
```json
AdminKey:	<admin_key>
```
**Request Body:**
```json
{
    "username": "User10",
    "password": "Pass10"
}
```

**Response Body:**
```json
{
    "username": "User10",
    "balance_usd": "8.00"
}
```
