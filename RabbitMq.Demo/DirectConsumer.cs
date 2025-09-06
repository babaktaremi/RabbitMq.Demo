using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using User.ProtoModels;

namespace RabbitMq.Demo;

public class DirectConsumer(IConnection connection, ILogger<DirectConsumer> logger)
{
    private IChannel? _channel;

    public async Task ConfigureAsync()
    {
        if (_channel == null || _channel.IsClosed)
            _channel = await connection.CreateChannelAsync();

        var consumer = new AsyncEventingBasicConsumer(_channel);

        await _channel.BasicConsumeAsync("demo.queue.grpc", false, consumer);

        consumer.ReceivedAsync += ConsumerOnReceivedAsync;
       // consumer.ReceivedAsync += ConsumerOnReceivedAsync2;
    }

    // private async Task ConsumerOnReceivedAsync2(object sender, BasicDeliverEventArgs @event)
    // {
    //     var data = Encoding.UTF8.GetString(@event.Body.ToArray());
    //     
    //     logger.LogWarning($"Direct message received from 2nd consumer: {data}");
    //     
    //     if (_channel != null) 
    //         await _channel.BasicNackAsync(@event.DeliveryTag, false, false);
    // }

    private async Task ConsumerOnReceivedAsync(object sender, BasicDeliverEventArgs @event)
    {
        try
        {
            var data = UserLoggedIn.Parser.ParseFrom(@event.Body.ToArray());

            logger.LogWarning("User Logged In With UserId:{UserId} ", data.UserId);

            if (_channel != null)
                await _channel.BasicAckAsync(@event.DeliveryTag, false, @event.CancellationToken);
        }
        catch (Exception e)
        {
           logger.LogError(e,"Could not consume message");
        }
    }

    // private async Task ConsumerOnReceivedAsync(object sender, BasicDeliverEventArgs @event)
    // {
    //     var data = Encoding.UTF8.GetString(@event.Body.ToArray());
    //     
    //     logger.LogWarning($"Direct message received: {data}");
    //
    //     if (_channel != null) 
    //         await _channel.BasicNackAsync(@event.DeliveryTag, false, false);
    // }
}