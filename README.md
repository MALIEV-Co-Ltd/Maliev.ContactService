# Maliev.MessageService

This repository contains the `Maliev.MessageService` project, migrated to a modern .NET 9 multi-project solution.

## Project Structure

The solution is composed of three main projects:

*   `Maliev.MessageService.Api`: The Web API project, responsible for handling HTTP requests and responses.
*   `Maliev.MessageService.Data`: A Class Library project for data access, containing the Entity Framework Core `DbContext` and entity models.
*   `Maliev.MessageService.Tests`: An xUnit Test Project, containing unit tests for the API and service layers.

## Getting Started

### Prerequisites

*   .NET 9 SDK (or later)
*   Visual Studio 2022 (recommended) or Visual Studio Code

### Build the Project

To build the entire solution, navigate to the root directory of the repository (`R:\maliev\Maliev.MessageService`) and run the following command:

```bash
dotnet build
```

### Run Tests

To run all unit tests, navigate to the root directory of the repository (`R:\maliev\Maliev.MessageService`) and run the following command:

```bash
dotnet test
```

### Run the API

To run the API locally, navigate to the `Maliev.MessageService.Api` project directory (`R:\maliev\Maliev.MessageService\Maliev.MessageService.Api`) and run the following command:

```bash
dotnet run
```

Alternatively, you can open the `Maliev.MessageService.sln` solution file in Visual Studio and run the `Maliev.MessageService.Api` project.

Once the API is running, the Swagger UI will be accessible at `http://localhost:<port>/messageservice/swagger` (the port number will be displayed in the console output when you run `dotnet run`).

## Configuration

### Local Development Secrets

For local development, sensitive information like connection strings and JWT keys are managed using User Secrets. To configure them:

1.  Right-click the `Maliev.MessageService.Api` project in Visual Studio.
2.  Select "Manage User Secrets".
3.  Paste the following content into the `secrets.json` file that opens, replacing the placeholder values with your actual secrets:

```json
{
  "JwtSecurityKey": "YOUR_JWT_SECURITY_KEY",
  "Jwt:Issuer": "maliev.com",
  "Jwt:Audience": "maliev.com",
  "ConnectionStrings:MessageServiceDbContext": "YOUR_LOCAL_DB_CONNECTION_STRING"
}
```

### Production Secrets

For production environments, secrets are managed via Google Secret Manager. Ensure the `ConnectionStrings-MessageServiceDbContext` secret is created and populated in your Google Cloud project (`maliev-website`).

```bash
gcloud secrets create "ConnectionStrings-MessageServiceDbContext" --project="maliev-website" --replication-policy="automatic" --labels="app=messageservice,env=production"
echo "your-production-db-connection-string" | gcloud secrets versions add "ConnectionStrings-MessageServiceDbContext" --project="maliev-website" --data-file=-
```