# Processes API & Worker

A minimal .NET 9 Web API and background worker for managing and executing long-running processes and subprocesses, using MongoDB for persistence and Hangfire for background job scheduling.

## Features

- RESTful API for creating, starting, cancelling, reverting, and resuming processes
- Subprocess and step management
- Background job execution with Hangfire and MongoDB
- OpenAPI/Swagger documentation
- Secure Hangfire dashboard (local access by default)
- **Automated API endpoint tests with xUnit**

## Project Structure

```
/Processes.sln
/Processes/              # Main API & Worker project
    Processes.csproj
    ...
/Processes.Tests/         # xUnit test project for API endpoints
    Processes.Tests.csproj
    ProcessesUnitTests.cs
```

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [MongoDB](https://www.mongodb.com/try/download/community)

## Getting Started

1. **Clone the repository:**

   ```powershell
   git clone <your-repo-url>
   cd Processes
   ```

2. **Configure environment variables:**

   - Copy `Processes/appsettings.json` or `Processes/appsettings.Development.json` and adjust as needed.
   - Set `MongoDbConnection` in your configuration or environment.
   - Optionally set `ApplicationRole` to `API`, `WORKER`, or `API_AND_WORKER`.

3. **Restore dependencies and build:**

   ```powershell
   dotnet restore
   dotnet build
   ```

4. **Run the application:**
   ```powershell
   dotnet run --project Processes/Processes.csproj
   ```
   - The API will be available at `https://localhost:5001` (default).
   - Swagger UI: `https://localhost:5001/swagger`
   - Hangfire Dashboard: `https://localhost:5001/hangfire`

## API Endpoints

- `POST   /processes` - Create a new process
- `GET    /processes` - List all processes
- `GET    /processes/{id}` - Get process by ID
- `POST   /processes/{id}/start` - Start a process
- `POST   /processes/{id}/cancel` - Cancel a running process
- `POST   /processes/{id}/revert` - Revert a cancelled/interrupted process
- `POST   /processes/{id}/resume` - Resume a not started/interrupted process
- `GET    /subprocesses` - List all subprocesses
- `GET    /subprocesses/{id}` - Get subprocess by ID
- `GET    /processes/{parentProcessId}/subprocesses` - List subprocesses for a process
- `GET    /health` - Health check
- `GET    /ready` - Readiness check
- `GET    /` - Root welcome

## Environment Variables

- `MongoDbConnection` - MongoDB connection string (default: `mongodb://localhost:27017`)
- `ApplicationRole` - Role for this instance: `API`, `WORKER`, or `API_AND_WORKER`

> **Tip:** Use a `.env` file or set environment variables in your shell. Document new variables in `.env.example`.

## Development & Testing

- Code style: C# conventions, see source for details.
- **Automated tests:**
  - xUnit tests for API endpoints are in `Processes.Tests/ProcessesUnitTests.cs`.
  - Logic/unit tests for core business logic are in `Processes.Tests/LogicUnitTests.cs` and use Moq for mocking dependencies.
  - Run all tests with:
    ```powershell
    dotnet test
    ```
  - Tests cover health, readiness, root, process and subprocess endpoints, error handling for invalid input, and core business logic.
- Unit and integration tests should be added for new features (minimum 80% coverage recommended).

## Security

- The Hangfire dashboard is restricted to local requests by default. Secure it before deploying to production.
- Do not hardcode sensitive information. Use environment variables or configuration files.

## Version Control

- Build artifacts in `bin/` and `obj/` are ignored via `.gitignore`.
- Follow [Conventional Commits](https://www.conventionalcommits.org/) for commit messages.
- Keep commits atomic and focused.

## Changelog

See [changelog.md](changelog.md) for release history and semantic versioning.

---

_Generated on 2025-05-23. Update this README as the project evolves._
