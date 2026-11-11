var builder = DistributedApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// MongoDB (container) + database. Nomes de recursos precisam ser únicos em todo o app model,
// por isso "mongo-idempotency" / "pg-idempotency" (o nome real do database é o 2º argumento).
// ---------------------------------------------------------------------------------------------
var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume();                                   // dados sobrevivem a restarts do AppHost

var mongoDb = mongo.AddDatabase("mongo-idempotency", "idempotency");

// ---------------------------------------------------------------------------------------------
// PostgreSQL (container) + database.
// ---------------------------------------------------------------------------------------------
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var postgresDb = postgres.AddDatabase("pg-idempotency", "idempotency");

// ---------------------------------------------------------------------------------------------
// Samples. WithReference injeta a connection string (ConnectionStrings__<nome>) e
// WaitFor garante que o banco esteja saudável antes de iniciar o serviço.
// ---------------------------------------------------------------------------------------------
builder.AddProject<Projects.Gs_MediatR_Idempotency_Samples_MongoDb>("sample-mongodb")
    .WithReference(mongoDb)
    .WaitFor(mongoDb)
    .WithEnvironment("Idempotency__Mode", "Lazy")        // modo do artigo: verifica → executa → grava
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Gs_MediatR_Idempotency_Samples_Postgres>("sample-postgres")
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithEnvironment("Idempotency__Mode", "Eager")       // reserva atômica → executa → grava
    .WithHttpHealthCheck("/health");

builder.Build().Run();
