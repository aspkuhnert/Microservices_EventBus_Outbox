using BuildingBlocks.EventBus;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Serilog;
using TransferService.Application;
using TransferService.Database;
using TransferService.Domain.IntegrationEvents;
using TransferService.Domain.Model;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .WriteTo.Console()
               .WriteTo.File("C:\\Source\\Experiments\\TransferLog.txt", rollingInterval: RollingInterval.Month, rollOnFileSizeLimit: true)
               .CreateLogger();

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<TransferContext>(options =>
{
   options.UseSqlServer(builder.Configuration.GetConnectionString("ConnectionString"));
});

builder.Services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

builder.Services.AddSingleton<IRabbitMqPersistentConnection>(serivices =>
{
   var logger = serivices.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();

   var factory = new ConnectionFactory()
   {
      HostName = builder.Configuration["EventBusConnection"],
      Port = 5672,
      VirtualHost = "/",
      DispatchConsumersAsync = true
   };

   if (!string.IsNullOrEmpty(builder.Configuration["EventBusUserName"]))
   {
      factory.UserName = builder.Configuration["EventBusUserName"];
   }

   if (!string.IsNullOrEmpty(builder.Configuration["EventBusPassword"]))
   {
      factory.Password = builder.Configuration["EventBusPassword"];
   }

   var retryCount = 5;
   if (!string.IsNullOrEmpty(builder.Configuration["EventBusRetryCount"]))
   {
      retryCount = int.Parse(builder.Configuration["EventBusRetryCount"]);
   }

   return new DefaultRabbitMQPersistentConnection(factory, logger, retryCount);
});

builder.Services.AddSingleton<IEventBus, EventBusRabbitMq>(services =>
{
   var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
   var subscriptionClientName = builder.Configuration["SubscriptionClientName"];
   var rabbitMQPersistentConnection = services.GetRequiredService<IRabbitMqPersistentConnection>();
   var logger = services.GetRequiredService<ILogger<EventBusRabbitMq>>();
   var eventBusSubcriptionsManager = services.GetRequiredService<IEventBusSubscriptionsManager>();

   var retryCount = 5;
   return new EventBusRabbitMq(
      scopeFactory,
      rabbitMQPersistentConnection,
      logger,
      eventBusSubcriptionsManager,
      subscriptionClientName,
      retryCount);
});

builder.Services.AddMediatR(typeof(Program).Assembly);

builder.Services.AddTransient<ITransferRepository, TransferRepository>();
builder.Services.AddTransient<ITransferManager, TransferManager>();

builder.Services.AddTransient<TransferCreatedIntegrationEventHandler>();

//var x = builder.Services.ToArray();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
   //using (var scope = app.Services.CreateScope())
   //{
   //   var db = scope.ServiceProvider.GetRequiredService<TransferContext>();
   //   db.Database.Migrate();
   //}

   app.UseSwagger();
   app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<TransferCreatedIntegrationEvent, TransferCreatedIntegrationEventHandler>();

app.Run();