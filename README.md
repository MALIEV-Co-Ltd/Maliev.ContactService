# Maliev Contact Service

[![Build Status](https://img.shields.io/badge/Build-Passing-success)](https://github.com/ORGANIZATION/Maliev.ContactService)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Database](https://img.shields.io/badge/Database-PostgreSQL%2018-blue)](https://www.postgresql.org/)

Customer inquiry and business communication management service for the Maliev public gateway.

**Role in MALIEV Architecture**: The primary entry point for external business inquiries. It captures contact form submissions, validates country data, handles file uploads for supporting documents, and provides an administrative interface for inquiry triage.

---

## 🏗️ Architecture & Tech Stack

- **Framework**: ASP.NET Core 10.0 (C# 13)
- **Database**: PostgreSQL 18 with Entity Framework Core 10.x
- **Distributed Cache**: Redis 7.x (Duplicate submission prevention & admin caching)
- **Messaging**: RabbitMQ via MassTransit
- **API Documentation**: OpenAPI 3.1 + Scalar UI
- **Observability**: OpenTelemetry (Metrics, Traces, Logging)

---

## ⚖️ Constitution Rules

This service strictly adheres to the platform development mandates:

### Banned Libraries
To maintain high performance and low complexity, the following are **NOT** used:
- ❌ **AutoMapper**: Explicit manual mapping only.
- ❌ **FluentValidation**: Standard Data Annotations (`[Required]`, `[EmailAddress]`) only.
- ❌ **FluentAssertions**: Standard xUnit `Assert` methods only.
- ❌ **In-memory Test DB**: All integration tests use **Testcontainers** with real PostgreSQL 18.

### Mandatory Practices
- ✅ **TreatWarningsAsErrors**: Enabled in all `.csproj` files.
- ✅ **XML Documentation**: Required on all public methods and properties.
- ✅ **No Secrets in Code**: All sensitive configuration injected via environment variables.
- ✅ **No Test Config in Program.cs**: Test configuration in test fixtures only.
- ✅ **IAM Integration**: Self-registers permissions with the IAM Service using GCP-style naming: `{service}.{resource}.{action}`.

---

## ✨ Key Features

- **Public Contact Gateway**: Secure submission of inquiries with country validation and anti-spam protection.
- **Inquiry Lifecycle Management**: Full administrative workflow from 'New' to 'Resolved' status.
- **Multimodal Support**: Seamless integration with UploadService for handling inquiry-related documents.
- **Spam Prevention**: 60-second sliding window duplicate detection per email address.
- **Priority Triage**: Automatic and manual prioritization of business-critical communications.

---

## 🚀 Quick Start

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for infrastructure)
- PostgreSQL 18 (Alpine)

### Local Development Setup

1. **Clone the repository**
```bash
git clone https://github.com/ORGANIZATION/Maliev.ContactService.git
cd Maliev.ContactService
```

2. **Spin up Infrastructure**
```bash
docker run --name contact-db -e POSTGRES_PASSWORD=YOUR_PASSWORD -p 5432:5432 -d postgres:18-alpine
docker run --name contact-redis -p 6379:6379 -d redis:7-alpine
```

3. **Configure Environment**
```powershell
# Windows PowerShell
$env:ConnectionStrings__ContactDbContext="YOUR_POSTGRES_CONNECTION_STRING"
$env:ConnectionStrings__Cache="YOUR_REDIS_CONNECTION_STRING"
```

4. **Apply Migrations & Run**
```bash
dotnet ef database update --project Maliev.ContactService.Api
dotnet run --project Maliev.ContactService.Api
```

The service will be available at `http://localhost:5000/contact`. Access the interactive documentation at `http://localhost:5000/contact/scalar`.

---

## 📡 API Endpoints

All endpoints are prefixed with `/contact/v1`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/contacts` | Submit a contact form |
| GET | `/contacts` | List inquiries (Admin only) |
| PUT | `/contacts/{id}/status` | Update inquiry status |
| GET | `/contacts/{id}/files` | List files attached to an inquiry |

---

## 🏥 Health & Monitoring

Standardized health probes for Kubernetes orchestration:
- **Liveness**: `GET /contact/liveness`
- **Readiness**: `GET /contact/readiness` (Checks DB and Redis connectivity)
- **Metrics**: `GET /contact/metrics` (Prometheus format)

---

## 🧪 Testing

We prioritize reliable tests over mock-heavy unit tests.

```bash
dotnet test --verbosity normal
```

- **Integration Tests**: Use real PostgreSQL 18 containers.
- **Contract Tests**: Ensure API stability for consumers.

---

## ✅ Validation and release boundary

Pull requests, `main`, `develop`, and `release/v*` tags run the same read-only
.NET validation workflow. Validation checks out immutable public revisions of
the MALIEV shared sources and restores only from NuGet.org, so it does not need
repository secrets or package credentials.

No workflow in this repository publishes images, authenticates to Google
Cloud, changes GitOps, or deploys to GKE. Release remains pending Aspire owner
review and must be introduced later as a separate, explicitly approved flow.

---

## 📄 License

Proprietary - © 2025 MALIEV Co., Ltd. All rights reserved.
