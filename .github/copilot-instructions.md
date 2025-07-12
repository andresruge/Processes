## Processes Project: AI Coding Agent Instructions

### Architecture & Major Components

- **Backend** (`Processes/`): .NET 9 Web API for process management, using MongoDB for persistence and Hangfire for background jobs. Key files: `Process.cs`, `ProcessJobExecutor.cs`, `StartupRecoveryService.cs`.
- **Frontend** (`Processes.Frontend/`): React + Vite + TypeScript UI. API URL is set via `VITE_API_URL` in `.env`.
- **Tests** (`Processes.Tests/`): xUnit for API and business logic. `ProcessesUnitTests.cs` covers endpoints; `LogicUnitTests.cs` covers core logic.

### Developer Workflows

- **Build & Run**
  - Backend: `dotnet restore; dotnet build; dotnet run --project Processes/Processes.csproj`
  - Frontend: `cd Processes.Frontend; npm install; npm run dev`
- **Testing**
  - Backend: `dotnet test`
  - Frontend: `cd Processes.Frontend; npm test` (if tests exist)
- **Environment**
  - Backend config: `Processes/appsettings.json` or `appsettings.Development.json`
  - Frontend: Copy `.env.example` to `.env` and set `VITE_API_URL`
  - Document new env vars in `README.md` and provide examples in `.env.example`

### Project-Specific Conventions

- **API endpoints**: See `README.md` for full list. Use RESTful patterns.
- **Roles**: Set `ApplicationRole` to `API`, `WORKER`, or `API_AND_WORKER` for different deployment modes.
- **Security**: Hangfire dashboard is local-only by default. Never hardcode secrets; use env vars.
- **Code Style**: C# conventions for backend, TypeScript/React for frontend. Add docstrings and comments for complex logic.
- **Version Control**: Use atomic commits, Conventional Commits, and update `.gitignore` for new build artifacts.

### Patterns & Integration

- **Background jobs**: Use Hangfire for process execution (`ProcessJobExecutor.cs`).
- **Persistence**: MongoDB via connection string in env/config.
- **Testing**: Minimum 80% coverage. Add unit/integration tests for new features. Use Moq for mocking in backend tests.
- **Change Logging**: Update `changelog.md` with each change, following semantic versioning.

### Examples

- To add a new API endpoint, follow the RESTful pattern in `Processes/ProcessRequest.cs` and add tests in `Processes.Tests/ProcessesUnitTests.cs`.
- To add a frontend feature, update `Processes.Frontend/src/components/`, and ensure API calls use the configured `VITE_API_URL`.
