using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.AspNetCore.Mvc;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;

var builder = WebApplication.CreateBuilder(args);

// Determine application role from configuration (e.g., appsettings.json)
var applicationRole = builder.Configuration["ApplicationRole"]?.ToUpperInvariant() ?? "API_AND_WORKER";
bool isApiEnabled = applicationRole.Contains("API");
bool isWorkerEnabled = applicationRole.Contains("WORKER");
// Configure JSON serialization to handle ObjectId properly
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new ObjectIdJsonConverter());
});

builder.Services.AddOpenApi();
// Enable Swagger/Scalar for API documentation
if (isApiEnabled)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDbConnection") ?? "mongodb://localhost:27017";
var mongoClient = new MongoClient(mongoConnectionString);
builder.Services.AddSingleton<IMongoClient>(mongoClient);

// Register our custom Job Executor
builder.Services.AddScoped<ProcessJobExecutor>();
// Configure Newtonsoft.Json settings for Hangfire
var hangfireJsonSettings = new Newtonsoft.Json.JsonSerializerSettings();
hangfireJsonSettings.Converters.Add(new ObjectIdNewtonsoftConverter()); // Add our custom converter

if (isWorkerEnabled || isApiEnabled) // Hangfire client (for enqueueing) might be needed by API, server by Worker
{
    // Configure Hangfire
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180) // Or latest compatible
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings(settings =>
        {
            settings.Converters.Add(new ObjectIdNewtonsoftConverter());
        }) // Pass your custom settings here
        .UseMongoStorage(mongoClient, databaseName: "LocalProcessesHangfire", new MongoStorageOptions // Using a separate DB or prefix for Hangfire collections
        {
            MigrationOptions = new MongoMigrationOptions
            {
                MigrationStrategy = new MigrateMongoMigrationStrategy(),
                BackupStrategy = new NoneMongoBackupStrategy() // Or CollectionMongoBackupStrategy for backups
            },
            Prefix = "hangfire", // Prefix for Hangfire collections
            CheckQueuedJobsStrategy = CheckQueuedJobsStrategy.TailNotificationsCollection // More responsive for job pickup
        }));

    // Add Hangfire server - this is what processes jobs
    if (isWorkerEnabled)
    {
        builder.Services.AddHangfireServer(options =>
        {
            options.WorkerCount = Environment.ProcessorCount * 2; // Adjust as needed
            options.Queues = ["default"]; // You can define multiple queues
        });
    }
}

// CORS policy for React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<StartupRecoveryService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<StartupRecoveryService>());

var app = builder.Build();
var database = mongoClient.GetDatabase("LocalProcesses");
var processesCollection = database.GetCollection<Process>("Processes");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.EnableTryItOutByDefault(); // Enable TryItOut for all endpoints
        options.DefaultModelsExpandDepth(-1); // Disable schema models to focus on endpoints
        options.SupportedSubmitMethods([Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Get, Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Post, Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Put, Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Delete]);
    });
}

if (isApiEnabled)
{
    // Use Hangfire Dashboard - only if API is enabled and Hangfire is configured
    if (isWorkerEnabled || isApiEnabled) // Check if Hangfire services were added
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            // IMPORTANT: Secure the dashboard in production!
            Authorization = [new LocalRequestsOnlyAuthorizationFilter()] // Example: Local access only
        });
    }

    app.UseHttpsRedirection();
    // Enable CORS for frontend
    app.UseCors("FrontendPolicy");

    app.MapGet("/", () => "Welcome to the Minimal Web API!").WithName("Root");

    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).WithName("Health");

    app.MapGet("/ready", ([FromServices] StartupRecoveryService svc) =>
    {
        if (svc.RecoveryComplete)
            return Results.Ok(new { ready = true });
        return Results.StatusCode(503);
    });

    app.MapPost("/processes", async (ProcessRequest request) =>
    {
        // Validate ProcessType
        if (!Enum.IsDefined(request.ProcessType))
        {
            return Results.BadRequest($"Invalid ProcessType: {request.ProcessType}");
        }
        // Validate number of subprocesses
        if (request.NumberOfSubprocesses < 1)
        {
            return Results.BadRequest("NumberOfSubprocesses must be at least 1.");
        }

        var newProcess = new Process
        {
            Name = request.Name,
            ItemsToProcess = request.NumberOfSubprocesses,
            Subprocesses = [],
            ProcessType = request.ProcessType
        };

        for (int i = 0; i < request.NumberOfSubprocesses; i++)
        {
            var subprocessId = ObjectId.GenerateNewId().ToString();
            newProcess.Subprocesses[subprocessId] = $"Subprocess {i + 1}";
        }

        await processesCollection.InsertOneAsync(newProcess);
        return Results.Created($"/processes/{newProcess.Id}", newProcess);
    }).WithName("CreateProcess");

    // Add an endpoint to list all processes
    app.MapGet("/processes", async () =>
    {
        var processes = await processesCollection.Find(_ => true).ToListAsync();
        return Results.Ok(processes);
    }).WithName("GetAllProcesses");

    // Add an endpoint to find a process by ID
    app.MapGet("/processes/{id}", async (string id) =>
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return Results.BadRequest("Invalid ID format.");
        }

        var process = await processesCollection.Find(p => p.Id == objectId).FirstOrDefaultAsync();
        return process is not null ? Results.Ok(process) : Results.NotFound();
    }).WithName("GetProcessById");

    // Update the endpoint to flag a Process as 'NotStarted' and update its timestamp
    app.MapPost("/processes/{id}/start", async (string id, IBackgroundJobClient backgroundJobClient) =>
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return Results.BadRequest("Invalid ID format.");
        }

        var process = await processesCollection.Find(p => p.Id == objectId).FirstOrDefaultAsync();
        if (process is null)
        {
            return Results.NotFound("Process not found.");
        }

        // Allow start if NotStarted, Reverted, Cancelled, or Interrupted
        if (process.Status == ProcessStatus.Running)
        {
            return Results.BadRequest("Process is already running.");
        }
        if (process.Status == ProcessStatus.Completed)
        {
            return Results.BadRequest("Process is already completed.");
        }

        // Check if there's an existing active Hangfire job
        if (!string.IsNullOrEmpty(process.HangfireJobId))
        {
            var jobData = JobStorage.Current?.GetMonitoringApi()?.JobDetails(process.HangfireJobId);
            if (jobData != null && (jobData.History[0].StateName == "Enqueued" || jobData.History[0].StateName == "Scheduled" || jobData.History[0].StateName == "Processing"))
            {
                return Results.BadRequest($"Process already has an active or queued job (Job ID: {process.HangfireJobId}).");
            }
        }

        // Enqueue the job with Hangfire
        var hangfireJobId = backgroundJobClient.Enqueue<ProcessJobExecutor>(executor => executor.ExecuteProcessJobAsync(objectId, false, JobCancellationToken.Null));

        // Update the Process status to 'NotStarted' (or 'Queued') and store HangfireJobId
        process.Status = ProcessStatus.NotStarted; // Hangfire job will set it to Running
        process.UpdatedAt = DateTime.UtcNow;
        process.HangfireJobId = hangfireJobId;
        process.ErrorMessage = null; // Clear previous errors
        await processesCollection.ReplaceOneAsync(p => p.Id == objectId, process);

        return Results.Ok($"Process has been queued with Hangfire (Job ID: {hangfireJobId}).");
    }).WithName("StartProcess");

    // Add an endpoint to cancel a running Process
    app.MapPost("/processes/{id}/cancel", async (string id, IBackgroundJobClient backgroundJobClient) =>
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return Results.BadRequest("Invalid ID format.");
        }

        var process = await processesCollection.Find(p => p.Id == objectId).FirstOrDefaultAsync();
        if (process is null) return Results.NotFound("Process not found.");

        // Add validation for terminal states
        if (process.Status == ProcessStatus.Completed)
        {
            return Results.BadRequest("Process is already completed. No cancellation action taken.");
        }
        if (process.Status == ProcessStatus.Cancelled)
        {
            return Results.BadRequest("Process is already cancelled.");
        }
        // Potentially add a check for ProcessStatus.Failed if you want to prevent cancellation attempts on failed jobs too.

        if (string.IsNullOrEmpty(process.HangfireJobId))
        {
            return Results.BadRequest("Process does not have an active Hangfire job to cancel. If it's NotStarted, you can revert or ignore.");
        }

        bool deleted = backgroundJobClient.Delete(process.HangfireJobId);
        // Deleting the job will trigger the CancellationToken in the ProcessJobExecutor if the job is running.
        // The ProcessJobExecutor's cancellation handling will update the process status.
        return deleted ? Results.Ok($"Cancellation request sent for Hangfire job {process.HangfireJobId}.")
                       : Results.BadRequest($"Could not cancel Hangfire job {process.HangfireJobId}. It might have already completed or failed.");
    }).WithName("CancelProcess");

    // Add an endpoint to revert a previously cancelled or interrupted Process
    app.MapPost("/processes/{id}/revert", async (string id) =>
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return Results.BadRequest("Invalid ID format.");
        }

        var process = await processesCollection.Find(p => p.Id == objectId).FirstOrDefaultAsync();
        if (process is null)
        {
            return Results.NotFound("Process not found.");
        }

        // Allow revert if Cancelled or Interrupted
        if (process.Status != ProcessStatus.Cancelled && process.Status != ProcessStatus.Interrupted)
        {
            return Results.BadRequest("Only cancelled or interrupted processes can be reverted.");
        }

        // Reset the Process status and UpdatedAt field
        process.Status = ProcessStatus.NotStarted;
        process.UpdatedAt = DateTime.UtcNow;
        await processesCollection.ReplaceOneAsync(p => p.Id == objectId, process);

        // Reset the status of existing Subprocesses and their Steps
        var subprocessCollection = database.GetCollection<Subprocess>("SubProcess");
        var subprocesses = await subprocessCollection.Find(s => s.ParentProcessId == objectId).ToListAsync();

        foreach (var subprocess in subprocesses)
        {
            subprocess.Status = ProcessStatus.NotStarted;
            subprocess.UpdatedAt = DateTime.UtcNow;

            foreach (var step in subprocess.Steps.Keys.ToList())
            {
                subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.NotStarted };
            }

            await subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess);
        }

        return Results.Ok("Process and its Subprocesses have been reverted to their original state.");
    }).WithName("RevertProcess");

    // Add an endpoint to resume an Interrupted or NotStarted Process (only resumes incomplete subprocesses/steps)
    app.MapPost("/processes/{id}/resume", async (string id, IBackgroundJobClient backgroundJobClient) =>
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return Results.BadRequest("Invalid ID format.");
        }

        var process = await processesCollection.Find(p => p.Id == objectId).FirstOrDefaultAsync();
        if (process is null)
        {
            return Results.NotFound("Process not found.");
        }

        // Only allow resume if NotStarted, Interrupted, or Cancelled
        if (process.Status == ProcessStatus.Running)
        {
            return Results.BadRequest("Process is already running.");
        }
        if (process.Status == ProcessStatus.Completed)
        {
            return Results.BadRequest("Process is already completed.");
        }

        // Check if there's an existing active Hangfire job
        if (!string.IsNullOrEmpty(process.HangfireJobId))
        {
            var jobData = JobStorage.Current?.GetMonitoringApi()?.JobDetails(process.HangfireJobId);
            if (jobData != null && (jobData.History[0].StateName == "Enqueued" || jobData.History[0].StateName == "Scheduled" || jobData.History[0].StateName == "Processing"))
            {
                return Results.BadRequest($"Process already has an active or queued job (Job ID: {process.HangfireJobId}). Cancel it first if you wish to resume differently.");
            }
        }

        // Enqueue the job with Hangfire for resuming
        var hangfireJobId = backgroundJobClient.Enqueue<ProcessJobExecutor>(executor => executor.ExecuteProcessJobAsync(objectId, true, JobCancellationToken.Null));

        process.Status = ProcessStatus.Interrupted; // Or NotStarted; Hangfire job will set it to Running
        process.UpdatedAt = DateTime.UtcNow;
        process.HangfireJobId = hangfireJobId;
        process.ErrorMessage = null; // Clear previous errors
        await processesCollection.ReplaceOneAsync(p => p.Id == objectId, process);
        return Results.Ok($"Process resume has been queued with Hangfire (Job ID: {hangfireJobId}).");
    }).WithName("ResumeProcess");

    // Add an endpoint to list all Subprocesses
    app.MapGet("/subprocesses", async () =>
    {
        var subprocessCollection = database.GetCollection<Subprocess>("SubProcess");
        var subprocesses = await subprocessCollection.Find(_ => true).ToListAsync();
        return Results.Ok(subprocesses);
    }).WithName("GetAllSubprocesses");

    // Add an endpoint to find a Subprocess by ID
    app.MapGet("/subprocesses/{id}", async (string id) =>
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return Results.BadRequest("Invalid ID format.");
        }

        var subprocessCollection = database.GetCollection<Subprocess>("SubProcess");
        var subprocess = await subprocessCollection.Find(s => s.Id == objectId).FirstOrDefaultAsync();
        return subprocess is not null ? Results.Ok(subprocess) : Results.NotFound();
    }).WithName("GetSubprocessById");

    // Add an endpoint to find Subprocesses by ParentProcessId
    app.MapGet("/processes/{parentProcessId}/subprocesses", async (string parentProcessId) =>
    {
        if (!ObjectId.TryParse(parentProcessId, out var objectId))
        {
            return Results.BadRequest("Invalid ParentProcessId format.");
        }

        var subprocessCollection = database.GetCollection<Subprocess>("SubProcess");
        var subprocesses = await subprocessCollection.Find(s => s.ParentProcessId == objectId).ToListAsync();
        return Results.Ok(subprocesses);
    }).WithName("GetSubprocessesByParentProcessId");
}

app.Run();

public partial class Program { }
