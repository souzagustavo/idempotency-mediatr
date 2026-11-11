using Gs.MediatR.Idempotency;
using Gs.MediatR.Idempotency.MongoDb;
using Gs.MediatR.Idempotency.Samples.Common;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry + health checks (ServiceDefaults do Aspire).
builder.AddServiceDefaults();

// Aspire client: registra o IMongoClient. A connection string vem do AppHost
// (WithReference) com o nome do recurso "mongo-idempotency".
builder.AddMongoDBClient("mongo-idempotency");

// MediatR + behavior + "broker" em memória + consumidor (código compartilhado em Samples.Common).
builder.Services.AddPaymentSample();

// Idempotência: opções vêm da seção "Idempotency" (appsettings / variáveis de ambiente) e o
// repositório é o MongoDB. O IRequestRepository usa o IMongoClient registrado acima.
builder.Services
    .AddIdempotency(options => builder.Configuration.GetSection("Idempotency").Bind(options))
    .UseMongoDb(options => options.DatabaseName = "idempotency");

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapSampleEndpoints("MongoDB");

app.Run();
