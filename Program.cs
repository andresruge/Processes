using MongoDB.Driver;
using MongoDB.Bson;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization to handle ObjectId properly
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new ObjectIdJsonConverter());
});

builder.Services.AddOpenApi();

// Enable Swagger/Scalar for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var mongoClient = new MongoClient("mongodb://localhost:27017");
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton<StartupRecoveryService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<StartupRecoveryService>());
builder.Services.AddHostedService<ProcessWorkerService>();
builder.Services.AddSingleton(new ConcurrentDictionary<ObjectId, CancellationTokenSource>());

var app = builder.Build();

var database = mongoClient.GetDatabase("LocalProcesses");
var processesCollection = database.GetCollection<Process>("Processes");

var cancellationTokenSources = app.Services.GetRequiredService<ConcurrentDictionary<ObjectId, CancellationTokenSource>>();

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

app.UseHttpsRedirection();

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
app.MapPost("/processes/{id}/start", async (string id) =>
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

    // Default to ProcessTypeA if missing (backward compatibility)
    var processType = process.GetType().GetProperty("ProcessType") != null ? process.ProcessType : ProcessType.ProcessTypeA;

    // Allow start if NotStarted, Reverted, Cancelled, or Interrupted
    if (process.Status == ProcessStatus.Running || process.Status == ProcessStatus.Completed)
    {
        return Results.BadRequest("Process is already running or completed.");
    }

    // Update the Process status to 'NotStarted' and update the UpdatedAt field
    process.Status = ProcessStatus.NotStarted;
    process.UpdatedAt = DateTime.UtcNow;
    await processesCollection.ReplaceOneAsync(p => p.Id == objectId, process);

    return Results.Ok("Process has been queued and will be started by the background worker.");
}).WithName("StartProcess");

// Add an endpoint to cancel a running Process
app.MapPost("/processes/{id}/cancel", (string id) =>
{
    if (!ObjectId.TryParse(id, out var objectId))
    {
        return Results.BadRequest("Invalid ID format.");
    }

    if (cancellationTokenSources.TryGetValue(objectId, out var cts))
    {
        cts.Cancel();
        return Results.Ok("Process cancellation requested.");
    }

    return Results.NotFound("No running process found with the specified ID.");
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
app.MapPost("/processes/{id}/resume", async (string id) =>
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
    if (process.Status == ProcessStatus.Running || process.Status == ProcessStatus.Completed)
    {
        return Results.BadRequest("Process is already running or completed.");
    }

    // Update the Process status to 'Running' and update the UpdatedAt field
    process.Status = ProcessStatus.Running;
    process.UpdatedAt = DateTime.UtcNow;
    await processesCollection.ReplaceOneAsync(p => p.Id == objectId, process);

    // Create a CancellationTokenSource for this Process
    var cts = new CancellationTokenSource();
    cancellationTokenSources[objectId] = cts;

    // Start resuming in the background
    _ = Task.Run(async () =>
    {
        var subprocessCollection = database.GetCollection<Subprocess>("SubProcess");
        try
        {
            await ProcessExecutionHelper.ExecuteProcessAsync(
                process,
                processesCollection,
                subprocessCollection,
                cancellationTokenSources,
                objectId,
                true,
                cts);
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation
            process.Status = ProcessStatus.Cancelled;
            process.UpdatedAt = DateTime.UtcNow;
            await processesCollection.ReplaceOneAsync(p => p.Id == objectId, process);
            // Update Subprocesses and their Steps to 'Cancelled'
            var childrenSubprocessCollection = database.GetCollection<Subprocess>("SubProcess");
            var subprocesses = await childrenSubprocessCollection.Find(s => s.ParentProcessId == objectId).ToListAsync();
            foreach (var subprocess in subprocesses)
            {
                if (subprocess.Status == ProcessStatus.Completed)
                    continue;
                subprocess.Status = ProcessStatus.Cancelled;
                subprocess.UpdatedAt = DateTime.UtcNow;
                foreach (var step in subprocess.Steps.Keys.ToList())
                {
                    if (subprocess.Steps[step].Status != ProcessStatus.Completed)
                    {
                        subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.Cancelled };
                    }
                }
                await childrenSubprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess);
            }
        }
        finally
        {
            cancellationTokenSources.TryRemove(objectId, out _);
        }
    });
    return Results.Ok("Process resume requested. Only incomplete subprocesses/steps will be resumed.");
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

app.Run();
