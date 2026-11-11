using Gs.MediatR.Idempotency;
using Gs.MediatR.Idempotency.Postgres;
using Gs.MediatR.Idempotency.Samples.Common;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry + health checks (ServiceDefaults do Aspire).
builder.AddServiceDefaults();

// Aspire client: registra o NpgsqlDataSource (com health check e telemetria). A connection string
// vem do AppHost (WithReference) com o nome do recurso "pg-idempotency".
builder.AddNpgsqlDataSource("pg-idempotency");

// MediatR + behavior + "broker" em memória + consumidor (código compartilhado em Samples.Common).
builder.Services.AddPaymentSample();

// Idempotência: opções vêm da seção "Idempotency" (appsettings / variáveis de ambiente) e o
// repositório é o PostgreSQL. A tabela é criada no startup (AutoCreateSchema = true).
builder.Services
    .AddIdempotency(options => builder.Configuration.GetSection("Idempotency").Bind(options))
    .UsePostgres(options =>
    {
        options.Schema = "public";
        options.TableName = "idempotency_requests";
    });

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapSampleEndpoints("PostgreSQL");

app.Run();
