using BankingService.Application;
using BankingService.Application.Behaviours;
using BankingService.Database;
using BankingService.Domain.IntegrationEvents;
using BankingService.Domain.Model;
using BuildingBlocks.EventBus;
using BuildingBlocks.IntegrationEventLog;
using BuildingBlocks.IntegrationEventLog.Database;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using RabbitMQ.Client;
using Serilog;
using System.Data.Common;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .WriteTo.Console()
               .WriteTo.File("C:\\Source\\Experiments\\BankingLog.txt", rollingInterval: RollingInterval.Month, rollOnFileSizeLimit: true)
               .CreateLogger();

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<BankingContext>(options =>
{
   options.UseSqlServer(
      builder.Configuration.GetConnectionString("ConnectionString"),
      sqlServerOptionsAction: sqlOptions =>
      {
         sqlOptions.MigrationsAssembly(typeof(Program).GetTypeInfo().Assembly.GetName().Name);
         //Configuring Connection Resiliency: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency
         sqlOptions.EnableRetryOnFailure(maxRetryCount: 15, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
      });
},
   ServiceLifetime.Scoped  //Showing explicitly that the DbContext is shared across the HTTP request scope (graph of objects started in the HTTP request)
);

builder.Services.AddDbContext<IntegrationEventLogContext>(options =>
{
   options.UseSqlServer(
      builder.Configuration.GetConnectionString("ConnectionString"),
      sqlServerOptionsAction: sqlOptions =>
      {
         sqlOptions.MigrationsAssembly(typeof(Program).GetTypeInfo().Assembly.GetName().Name);
         //Configuring Connection Resiliency: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency
         sqlOptions.EnableRetryOnFailure(maxRetryCount: 15, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
      });
});

builder.Services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

builder.Services.AddTransient<Func<DbConnection, IIntegrationEventLogService>>(services => (DbConnection connection) => new IntegrationEventLogService(connection));
builder.Services.AddTransient<IBankingIntegrationEventService, BankingIntegrationEventService>();

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
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehaviour<,>));

builder.Services.AddTransient<IAccountRepository, AccountRepository>();
builder.Services.AddTransient<IAccountManager, AccountManager>();

var app = builder.Build();

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment())
{
   // AK - Creepy shortcut to create the database during the first run, set to true in this case
   var migrate = false;

   if (migrate)
   {
      using (var scope = app.Services.CreateScope())
      {
         var bankingContext = scope.ServiceProvider.GetRequiredService<BankingContext>();
         var bankingLogger = scope.ServiceProvider.GetRequiredService<ILogger<BankingContext>>();

         var retries = 10;

         try
         {
            var retry = Policy.Handle<SqlException>()
               .WaitAndRetry(
                  retryCount: retries,
                  sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                  onRetry: (exception, timeSpan, retry, ctx) =>
                  {
                     bankingLogger.LogWarning(exception, "[{prefix}] Exception {ExceptionType} with message {Message} detected on attempt {retry} of {retries}", nameof(BankingContext), exception.GetType().Name, exception.Message, retry, retries);
                  });

            //if the sql server container is not created on run docker compose this
            //migration can't fail for network related exception. The retry options for DbContext only
            //apply to transient exceptions
            // Note that this is NOT applied when running some orchestrators (let the orchestrator to recreate the failing service
            retry.Execute(() => InvokeSeeder(bankingContext, app.Services));
         }
         catch (Exception ex)
         {
            bankingLogger.LogError(ex, "An error occurred while migrating the database used on context {DbContextName}", typeof(BankingContext).Name);
         }

         var integrationContext = scope.ServiceProvider.GetRequiredService<IntegrationEventLogContext>();
         var integrationLogger = scope.ServiceProvider.GetRequiredService<ILogger<IntegrationEventLogContext>>();

         try
         {
            var retry = Policy.Handle<SqlException>()
              .WaitAndRetry(
                  retryCount: retries,
                  sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                  onRetry: (exception, timeSpan, retry, ctx) =>
                  {
                     integrationLogger.LogWarning(exception, "[{prefix}] Exception {ExceptionType} with message {Message} detected on attempt {retry} of {retries}", nameof(IntegrationEventLogContext), exception.GetType().Name, exception.Message, retry, retries);
                  });

            //if the sql server container is not created on run docker compose this
            //migration can't fail for network related exception. The retry options for DbContext only
            //apply to transient exceptions
            // Note that this is NOT applied when running some orchestrators (let the orchestrator to recreate the failing service)
            retry.Execute(() => InvokeSeeder(integrationContext, app.Services));
         }
         catch (Exception ex)
         {
            integrationLogger.LogError(ex, "An error occurred while migrating the database used on context {DbContextName}", typeof(IntegrationEventLogContext).Name);
         }
      }
   }

   app.UseSwagger();
   app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

static void InvokeSeeder<TContext>(TContext context, IServiceProvider services)
   where TContext : DbContext
{
   context.Database.Migrate();
}