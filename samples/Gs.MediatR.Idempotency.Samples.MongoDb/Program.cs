using Gs.MediatR.Idempotency;
using Gs.MediatR.Idempotency.MongoDb;
using Gs.MediatR.Idempotency.Samples.Common;
using Medallion.Threading;
using Medallion.Threading.MongoDB;
using MongoDB.Driver;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddMongoDBClient("mongo-idempotency");

// MediatR + behavior + "broker" em memória + consumidor (código compartilhado em Samples.Common).
builder.Services.AddPaymentSample();

// Idempotência: opções vêm da seção "Idempotency" (appsettings / variáveis de ambiente) e o
// repositório é o MongoDB. O IRequestRepository usa o IMongoClient registrado acima.
builder.Services
    .AddMediatRIdempotency(options => builder.Configuration.GetSection("IdempotencyMediatR").Bind(options))
    .UseMongoDb(options => options.DatabaseName = "idempotency");

builder.Services.AddScoped<IDistributedLockProvider>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new MongoDistributedSynchronizationProvider(db);
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapSampleEndpoints("MongoDB");

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "MediatR Idempotency - MongoDB Sample";
    options.Theme = Scalar.AspNetCore.ScalarTheme.Mars;
});

app.Run();
