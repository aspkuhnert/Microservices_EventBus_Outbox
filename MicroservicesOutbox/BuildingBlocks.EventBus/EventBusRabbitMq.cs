using BuildingBlocks.EventBus.Events;
using BuildingBlocks.EventBus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace BuildingBlocks.EventBus
{
   public class EventBusRabbitMq :
      IEventBus,
      IDisposable
   {
      private const string BROKER_NAME = "simple_event_bus";

      private readonly Dictionary<string, List<Type>> _handlers;
      private readonly List<Type> _eventTypes;

      private readonly IServiceScopeFactory _serviceScopeFactory;
      private readonly IRabbitMqPersistentConnection _persistentConnection;
      private readonly ILogger<EventBusRabbitMq> _logger;
      private readonly IEventBusSubscriptionsManager _subsManager;
      private readonly int _retryCount;

      private IModel _consumerChannel;
      private string _queueName;

      public EventBusRabbitMq(
         IServiceScopeFactory serviceScopeFactory,
         IRabbitMqPersistentConnection persistentConnection,
         ILogger<EventBusRabbitMq> logger,
         IEventBusSubscriptionsManager subsManager,
         string queueName = null,
         int retryCount = 5)
      {
         _serviceScopeFactory = serviceScopeFactory;
         _handlers = new Dictionary<string, List<Type>>();
         _eventTypes = new List<Type>();

         _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
         _logger = logger ?? throw new ArgumentNullException(nameof(logger));
         _subsManager = subsManager ?? new InMemoryEventBusSubscriptionsManager();
         _queueName = queueName;
         _consumerChannel = CreateConsumerChannel();
         _retryCount = retryCount;
         _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
      }

      private void SubsManager_OnEventRemoved(object sender, string eventName)
      {
         if (!_persistentConnection.IsConnected)
         {
            _persistentConnection.TryConnect();
         }

         using var channel = _persistentConnection.CreateModel();
         channel.QueueUnbind(queue: _queueName,
             exchange: BROKER_NAME,
             routingKey: eventName);

         if (_subsManager.IsEmpty)
         {
            _queueName = string.Empty;
            _consumerChannel.Close();
         }
      }

      public void Publish(IntegrationEvent @event)
      {
         if (!_persistentConnection.IsConnected)
         {
            _persistentConnection.TryConnect();
         }

         var policy = RetryPolicy.Handle<BrokerUnreachableException>()
             .Or<SocketException>()
             .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
             {
                _logger.LogWarning(ex, "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
             });

         var eventName = @event.GetType().Name;

         _logger.LogTrace("Creating RabbitMQ channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

         using var channel = _persistentConnection.CreateModel();
         _logger.LogTrace("Declaring RabbitMQ exchange to publish event: {EventId}", @event.Id);

         channel.ExchangeDeclare(exchange: BROKER_NAME, type: "direct");

         var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), new JsonSerializerOptions
         {
            WriteIndented = true
         });

         policy.Execute(() =>
         {
            var properties = channel.CreateBasicProperties();
            properties.DeliveryMode = 2; // persistent

            _logger.LogTrace("Publishing event to RabbitMQ: {EventId}", @event.Id);

            channel.BasicPublish(
                exchange: BROKER_NAME,
                routingKey: eventName,
                mandatory: true,
                basicProperties: properties,
                body: body);
         });
      }

      public void Subscribe<TIntegrationEvent, TIntegrationEventHandler>()
         where TIntegrationEvent : IntegrationEvent
         where TIntegrationEventHandler : IIntegrationEventHandler<TIntegrationEvent>
      {
         var eventName = _subsManager.GetEventKey<TIntegrationEvent>();
         DoInternalSubscription(eventName);

         _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TIntegrationEventHandler).GetGenericTypeName());

         _subsManager.AddSubscription<TIntegrationEvent, TIntegrationEventHandler>();
         StartBasicConsume();
      }

      private void DoInternalSubscription(string eventName)
      {
         var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);

         if (!containsKey)
         {
            if (!_persistentConnection.IsConnected)
            {
               _persistentConnection.TryConnect();
            }

            _consumerChannel.QueueBind(
               queue: _queueName,
               exchange: BROKER_NAME,
               routingKey: eventName);
         }
      }

      private void StartBasicConsume()
      {
         _logger.LogTrace("Starting RabbitMQ basic consume");

         if (_consumerChannel != null)
         {
            var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

            consumer.Received += Consumer_Received;

            _consumerChannel.BasicConsume(
                queue: _queueName,
                autoAck: false,
                consumer: consumer);
         }
         else
         {
            _logger.LogError("StartBasicConsume can't call on _consumerChannel == null");
         }
      }

      private async Task Consumer_Received(object sender, BasicDeliverEventArgs e)
      {
         var eventName = e.RoutingKey;
         var message = Encoding.UTF8.GetString(e.Body.ToArray());

         try
         {
            await ProcessEvent(eventName, message).ConfigureAwait(false);
         }
         catch (Exception ex)
         {
         }
      }

      private async Task ProcessEvent(string eventName, string message)
      {
         if (_subsManager.HasSubscriptionsForEvent(eventName))
         {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
               var subscriptions = _subsManager.GetHandlersForEvent(eventName);

               foreach (var subscription in subscriptions)
               {
                  try
                  {
                     var handler = scope.ServiceProvider.GetRequiredService(subscription.HandlerType);
                     var eventType = _subsManager.GetEventTypeByName(eventName);
                     var integrationEvent = JsonSerializer.Deserialize(message, eventType, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                     var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

                     await Task.Yield();
                     await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });
                  }
                  catch (Exception ex)
                  {
                     _logger.LogWarning("No registered event handler for RabbitMQ event: {EventName}", eventName);
                     continue;
                  }
               }
            }
         }
         else
         {
            _logger.LogWarning("No subscription for RabbitMQ event: {EventName}", eventName);
         }
      }

      private IModel CreateConsumerChannel()
      {
         if (!_persistentConnection.IsConnected)
         {
            _persistentConnection.TryConnect();
         }

         _logger.LogTrace("Creating RabbitMQ consumer channel");

         var channel = _persistentConnection.CreateModel();

         channel.ExchangeDeclare(exchange: BROKER_NAME,
                                 type: "direct");

         channel.QueueDeclare(queue: _queueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

         channel.CallbackException += (sender, ea) =>
         {
            _logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");

            _consumerChannel.Dispose();
            _consumerChannel = CreateConsumerChannel();
            StartBasicConsume();
         };

         return channel;
      }

      public void Dispose()
      {
         if (_consumerChannel != null)
         {
            _consumerChannel.Dispose();
         }

         _subsManager.Clear();
      }
   }
}