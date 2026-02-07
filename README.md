# Marketplace API

## Overview
Marketplace API is a **backend-only, role-based marketplace system** built to demonstrate **real-world business rules**, **approval workflows**, and **enterprise-grade backend practices**.

The system intentionally has **no frontend**. It focuses on clean API design, security, auditability, and operational readiness using modern backend tooling.

---

## Tech Stack

- **.NET 8 Web API** – Core backend framework
- **Keycloak** – Authentication & Role-Based Access Control (RBAC)
- **PostgreSQL** – Relational data store
- **Docker & Docker Compose** – Local and cloud-ready deployment
- **Serilog** – Structured logging with correlation IDs

---

## Roles & Responsibilities

### Admin
- Reviews seller requests for category approval
- Approves or rejects requests (with reason)
- Full audit visibility of approval decisions

### Seller
- Requests approval to sell in specific categories
- Creates products **only in approved categories**
- Views feedback left by buyers on own products

### Buyer
- Browses products
- Leaves feedback (rating + comment) on products
- One feedback per buyer per product

---

## Core Business Rules

1. Sellers **cannot sell in a category without admin approval**
2. Category approval is **per seller, per category**
3. Admin decisions are **audited and traceable**
4. Product creation is **blocked if approval does not exist**
5. Buyers can leave feedback; sellers can only view feedback for their products

These rules are enforced at both **API** and **database** levels.

---

## High-Level Functional Flow

### Seller Category Approval Flow
1. Seller submits a category approval request
2. Request is stored as `Pending`
3. Admin reviews the request
4. Admin approves or rejects with reason
5. Approved sellers can create products in that category

### Product Creation Flow
1. Seller attempts to create a product
2. System validates category approval
3. Product is created only if approval exists

### Buyer Feedback Flow
1. Buyer submits feedback for a product
2. Feedback is linked to buyer and product
3. Seller can view feedback for own products only

---

========================================================
MARKETPLACE API – FINAL COMPLETE API LIST
========================================================


--------------------------------------------------------
00 – Authenticated (Any Credential)
--------------------------------------------------------
GET  /api/me
GET  /api/whoami
GET  /api/claims
GET  /api/validate-token


--------------------------------------------------------
10 – Admin
--------------------------------------------------------

POST /api/admin/users/create-seller
     Create a new seller user and assign seller role

GET  /api/admin/category-requests/pending
     View all pending category approval requests from sellers

POST /api/admin/category-requests/{id}/review
     Approve or reject a seller category request
     (Updates Status, ApprovedUtc, ApprovedBy, RejectionReason)

GET  /api/admin/category-requests/approved
     View all approved seller-category mappings (global audit view)


--------------------------------------------------------
20 – Seller
--------------------------------------------------------

POST /api/seller/category-requests
     Request approval to sell in a category

GET  /api/seller/category-requests
     View status of seller’s category requests

GET  /api/seller/approved-categories
     View categories approved for this seller

GET  /api/seller/products
     View seller products (list, count, total value)

POST /api/seller/products
     Add a new product (only if category is approved)

PUT  /api/seller/products/{id}
     Update product price or stock quantity

GET  /api/seller/feedback
     View feedback for seller’s products


--------------------------------------------------------
30 – Buyer
--------------------------------------------------------

GET  /api/products
     Browse all approved products

GET  /api/products/{id}
     View product details

POST /api/orders
     Create a new order

GET  /api/orders
     View buyer’s orders

POST /api/products/{id}/feedback
     Submit feedback for a purchased product


--------------------------------------------------------
90 – Public / Tools
--------------------------------------------------------

GET  /health
     Service health check

GET  /swagger
     Swagger endpoint

GET  /swagger/index.html
     Swagger UI

GET  /api/tools/ping
     Connectivity test

GET  /api/tools/version
     API version information


========================================================
END OF API LIST
========================================================

---

## Logging & Observability

The system uses **Serilog** for structured logging:
- Request-level logs
- Correlation IDs (`X-Correlation-Id`)
- User context (UserSub, Username)
- Console + rolling file logs

Designed to integrate easily with centralized log systems.

---

## Database Source of Truth

- `schema.sql` is the **authoritative database definition**
- Automatically applied via Docker on first startup
- Prevents schema drift and runtime DB errors

---

## Running the Project

### Prerequisites
- Docker
- Docker Compose

### Start the system
```bash
docker compose up -d
```

### Reset database (fresh schema)
```bash
./scripts/reset-db.ps1
```

---

## Security Model

- Authentication handled by Keycloak (JWT)
- Authorization enforced via `[Authorize(Roles = ...)]`
- Business rule validation enforced in application logic

---

## What This Project Is

- A **reference-quality backend system**
- A demonstration of **enterprise backend patterns**
- Suitable for architecture discussions, interviews, and templates

## What This Project Is NOT

- No frontend UI
- No payment gateway
- No order fulfillment
- Not a full e-commerce platform

---

## Ideal Use Cases

- Backend architecture interviews
- RBAC and approval workflow demos
- Starter template for real-world systems
- Portfolio / reference project

---

## License

Educational / reference use.

