var builder = DistributedApplication.CreateBuilder(args);

// 1. Setup Containers
var sql = builder.AddSqlServer("sql").AddDatabase("sqldata");
var mongo = builder.AddMongoDB("mongo").AddDatabase("mongodata");
var postgres = builder.AddPostgres("postgres").AddDatabase("postgresdata");
var redis = builder.AddRedis("redis");
var rabbitmq = builder.AddRabbitMQ("rabbitmq");

// 2. Setup .NET Services
var bookingService = builder.AddProject<Projects.BookingService>("bookingservice")
    .WithReference(sql)
    .WithReference(redis)
    .WithReference(rabbitmq)
    .WaitFor(sql)
    .WaitFor(redis)
    .WaitFor(rabbitmq);

var apiGateway = builder.AddProject<Projects.ApiGateway>("apigateway")
    .WithReference(bookingService);

// 3. Setup Node.js Services (catalog, payment, frontend)
var catalogService = builder.AddNpmApp("catalogservice", "../catalog-service", "start:dev")
    .WithReference(mongo)
    .WithReference(redis)
    .WaitFor(mongo)
    .WaitFor(redis)
    .WithEnvironment("PORT", "3001");

var paymentService = builder.AddNpmApp("paymentservice", "../payment-service", "start")
    .WithReference(postgres)
    .WithReference(rabbitmq)
    .WaitFor(postgres)
    .WaitFor(rabbitmq)
    .WithEnvironment("PORT", "3002");

var frontend = builder.AddNpmApp("frontend", "../frontend", "dev")
    .WithReference(apiGateway)
    .WaitFor(apiGateway)
    .WithEnvironment("PORT", "3000");

// Expose internal endpoints to ApiGateway if needed
apiGateway.WithReference(catalogService);

builder.Build().Run();
