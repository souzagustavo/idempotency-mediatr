var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo", port: 27017)
    .WithDataVolume();                                   

var mongoDb = mongo.AddDatabase("mongo-idempotency", "idempotency");

builder.AddProject<Projects.Gs_MediatR_Idempotency_Samples_MongoDb>("sample-mongodb")
    .WithReference(mongoDb)
    .WaitFor(mongoDb)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
