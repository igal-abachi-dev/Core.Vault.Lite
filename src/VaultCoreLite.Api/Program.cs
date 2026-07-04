using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using VaultCoreLite.Api;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using VaultCoreLite.Application.Abstractions;
using VaultCoreLite.Application.Services;
using VaultCoreLite.Infrastructure.Persistence;
using VaultCoreLite.Infrastructure.Repositories;
using VaultCoreLite.ProductRuntime.Plugin;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("core-api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 600,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddDbContext<VaultDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("VaultDb"));
});

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ILedgerRepository, LedgerRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IContractExecutionRepository, ContractExecutionRepository>();
builder.Services.AddScoped<ISimulationRepository, SimulationRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<PostingService>();
builder.Services.AddScoped<SchedulerService>();
builder.Services.AddScoped<SimulationService>();
builder.Services.AddHostedService<SchedulerBackgroundService>();
builder.Services.AddProductRuntime(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/readyz", async (VaultDbContext db, CancellationToken ct) =>
{
    var ok = await db.Database.CanConnectAsync(ct);
    return ok ? Results.Ok(new { status = "ready" }) : Results.Problem("Database is not reachable.");
});

app.UseRateLimiter();

var v1 = app.MapGroup("/v1").RequireRateLimiting("core-api");

v1.MapPost("/products", async (CreateProductRequest request, ProductService service, CancellationToken ct) => Results.Created("/v1/products", await service.CreateAsync(request, ct)));

v1.MapPost("/product-versions", async (CreateProductVersionRequest request, ProductService service, CancellationToken ct) => Results.Created("/v1/product-versions", await service.CreateVersionAsync(request, ct)));

v1.MapPost("/product-versions/{id:guid}:activate", async (Guid id, ProductService service, CancellationToken ct) =>
{
    await service.ActivateVersionAsync(id, ct);
    return Results.Ok(new { id, status = "ACTIVE" });
});

v1.MapPost("/accounts", async (CreateAccountRequest request, AccountService service, CancellationToken ct) => Results.Created("/v1/accounts", await service.CreateAsync(request, "api", ct)));

v1.MapGet("/accounts/{id:guid}", async (Guid id, AccountService service, CancellationToken ct) =>
{
    var account = await service.GetAsync(id, ct);
    return account is null ? Results.NotFound() : Results.Ok(account);
});

v1.MapPost("/posting-instruction-batches", async (VaultCoreLite.Domain.Ledger.BatchRequest request, PostingService service, CancellationToken ct) => Results.Ok(await service.PostBatchAsync(request, ct)));

v1.MapPost("/simulations/transaction", async (VaultCoreLite.Domain.Ledger.SimulateTransactionRequest request, SimulationService simulations, CancellationToken ct) =>
    Results.Created("/v1/simulations", await simulations.SimulateTransactionAsync(request, ct)));

v1.MapGet("/simulations/{id:guid}", async (Guid id, SimulationService simulations, CancellationToken ct) =>
{
    var simulation = await simulations.GetAsync(id, ct);
    return simulation is null ? Results.NotFound() : Results.Ok(simulation);
});

v1.MapPost("/simulations/{id:guid}:confirm", async (Guid id, VaultCoreLite.Domain.Ledger.ConfirmSimulationRequest request, SimulationService simulations, CancellationToken ct) =>
    Results.Ok(await simulations.ConfirmAndExecuteAsync(id, request, ct)));


v1.MapGet("/accounts/{id:guid}/balances", async (Guid id, ILedgerRepository ledger, CancellationToken ct) => Results.Ok(new { accountId = id, balances = await ledger.ListBalancesAsync(id, ct) }));

v1.MapGet("/accounts/{id:guid}/postings", async (Guid id, int? limit, ILedgerRepository ledger, CancellationToken ct) => Results.Ok(new { accountId = id, postings = await ledger.ListPostingsAsync(id, limit ?? 100, ct) }));

v1.MapGet("/client-transactions/{clientId}/{clientTransactionId}", async (string clientId, string clientTransactionId, ILedgerRepository ledger, CancellationToken ct) =>
{
    var tx = await ledger.GetClientTransactionAsync(clientId, clientTransactionId, ct);
    return tx is null ? Results.NotFound() : Results.Ok(tx);
});

v1.MapGet("/events", async (long? afterSeq, int? limit, IOutboxRepository outbox, CancellationToken ct) => Results.Ok(new { events = await outbox.ReadAsync(afterSeq ?? 0, limit ?? 100, ct) }));

v1.MapGet("/trial-balance", async (string? denomination, ILedgerRepository ledger, CancellationToken ct) => Results.Ok(await ledger.TrialBalanceAsync(denomination, ct)));

v1.MapGet("/audit/invariants", async (ILedgerRepository ledger, CancellationToken ct) => Results.Ok(await ledger.AuditInvariantsAsync(ct)));

v1.MapPost("/schedules/daily", async (CreateDailyScheduleRequest request, SchedulerService scheduler, CancellationToken ct) => Results.Created("/v1/schedules/daily", await scheduler.CreateDailyScheduleAsync(request, ct)));

v1.MapPost("/schedules", async (CreateScheduleRequest request, SchedulerService scheduler, CancellationToken ct) => Results.Created("/v1/schedules", await scheduler.CreateScheduleAsync(request, ct)));

v1.MapPost("/scheduler/run-due", async (SchedulerService scheduler, CancellationToken ct) => Results.Ok(new { processed = await scheduler.RunDueAsync(DateTimeOffset.UtcNow, 100, ct) }));

app.Run();
