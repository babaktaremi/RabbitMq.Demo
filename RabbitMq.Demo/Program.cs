using System.Text;
using RabbitMQ.Client;
using RabbitMq.Demo;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionFactory = new ConnectionFactory()
{
    Uri = new Uri("amqp://admin:admin123@localhost:5672/"),
    ContinuationTimeout = TimeSpan.FromSeconds(5),
    ClientProvidedName = "DemoApi",

};

var connection =await connectionFactory.CreateConnectionAsync();


await using (var channel = await connection.CreateChannelAsync())
{
    await channel.ExchangeDeclareAsync("demo.direct", ExchangeType.Direct, true, false, passive: false);
await channel.ExchangeDeclareAsync("demo.topics", ExchangeType.Topic, true, false, passive: false);
await channel.ExchangeDeclareAsync("demo.headers", ExchangeType.Headers, true, false, passive: false);
await channel.ExchangeDeclareAsync("demo.fanout", ExchangeType.Fanout, true, false, passive: false);
await channel.ExchangeDeclareAsync("demo.deadletter", ExchangeType.Direct, true, false, passive:false);

await channel.ExchangeDeclareAsync("demo.exception", ExchangeType.Fanout, true, false, passive: false);

await channel.QueueDeclareAsync("demo.queue.direct", false, false, false);
await channel.QueueDeclareAsync("demo.queue.topic.1", false, false, false);
await channel.QueueDeclareAsync("demo.queue.topic.2", false, false, false);
await channel.QueueDeclareAsync("demo.queue.headers.1", false, false, false);
await channel.QueueDeclareAsync("demo.queue.headers.2", false, false, false);
await channel.QueueDeclareAsync("demo.queue.fanout.1", false, false, false);
await channel.QueueDeclareAsync("demo.queue.fanout.2", false, false, false);
await channel.QueueDeclareAsync("demo.queue.deadletter", true, false, false);
await channel.QueueDeclareAsync("demo.queue.exception", true, false, false,new Dictionary<string, object?>()
{
    {"x-dead-letter-exchange","demo.deadletter"}
});


await channel.QueueBindAsync("demo.queue.direct", "demo.direct","");

await channel.QueueBindAsync("demo.queue.topic.1", "demo.topics", "one.#");
await channel.QueueBindAsync("demo.queue.topic.2", "demo.topics", "two.#");

var headers1 = new Dictionary<string, object>()
{
    { "queue-name", "demo.queue.headers.1" },
};

var headers2 = new Dictionary<string, object>()
{
    { "queue-name", "demo.queue.headers.2" },
};

await channel.QueueBindAsync("demo.queue.headers.1", "demo.headers", "", arguments: headers1!);
await channel.QueueBindAsync("demo.queue.headers.2", "demo.headers", "", arguments: headers2!);


await channel.QueueBindAsync("demo.queue.fanout.1", "demo.fanout", "");
await channel.QueueBindAsync("demo.queue.fanout.2", "demo.fanout", "");

await channel.QueueBindAsync("demo.queue.deadletter", "demo.deadletter", "");

await channel.QueueBindAsync("demo.queue.exception", "demo.exception", "");
}



builder.Services.AddSingleton(connection);

builder.Services.AddTransient<ProducerChannelProvider>();

var app = builder.Build();

app.MapPost("/DirectPublish", async (ProducerChannelProvider channelProvider) =>
{
    var message = "This is direct publish"u8.ToArray();

    var properties = new BasicProperties()
    {
        ContentType = "text/plain",
        DeliveryMode = DeliveryModes.Persistent,
    };

    await using var rabbitMqChannel=await channelProvider.GetChannelAsync();

    await rabbitMqChannel.BasicPublishAsync("demo.direct", "", true, properties, message);
});


app.MapPost("/TopicPublish", async (ProducerChannelProvider channelProvider, string routingKey) =>
{
    var message = Encoding.UTF8.GetBytes($"This is topic publish with topic {routingKey}");

    var properties = new BasicProperties()
    {
        ContentType = "text/plain",
        DeliveryMode = DeliveryModes.Persistent,
    };

    var rabbitMqChannel = await channelProvider.GetChannelAsync();
    
    await rabbitMqChannel.BasicPublishAsync("demo.topics", routingKey, true, properties, message);
});

app.MapPost("/HeadersPublish", async (ProducerChannelProvider channelProvider, string queueName) =>
{
    var message = Encoding.UTF8.GetBytes($"This is header publish with queueName {queueName}");

    var properties = new BasicProperties()
    {
        ContentType = "text/plain",
        DeliveryMode = DeliveryModes.Persistent,
        Headers = new Dictionary<string, object?>()
        {
            { "queue-name", queueName }
        }
    };

    var rabbitMqChannel = await channelProvider.GetChannelAsync();
    
    await rabbitMqChannel.BasicPublishAsync("demo.headers", "", true, properties, message);
});

app.MapPost("/FanoutPublish", async (ProducerChannelProvider channelProvider) =>
{
    var message = "This is fanout publish"u8.ToArray();

    var properties = new BasicProperties()
    {
        ContentType = "text/plain",
        DeliveryMode = DeliveryModes.Persistent,
    };

    var rabbitMqChannel = await channelProvider.GetChannelAsync();
    
    await rabbitMqChannel.BasicPublishAsync("demo.fanout", "", true, properties, message);
});


app.MapPost("/ExceptionMessage", async (ProducerChannelProvider channelProvider) =>
{
    var message = "This is exception publish and must go to dead letter exchange"u8.ToArray();

    var properties = new BasicProperties()
    {
        ContentType = "text/plain",
        DeliveryMode = DeliveryModes.Persistent,
        Expiration = "5000"
    };

    var rabbitMqChannel = await channelProvider.GetChannelAsync();
    
    await rabbitMqChannel.BasicPublishAsync("demo.exception", "", true, properties, message);
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();



app.Run();
