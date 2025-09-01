using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace RabbitMq.Demo;

public class ProducerChannelProvider(IConnection connection):IDisposable
{
    private static readonly ConcurrentBag<IChannel>  ChannelPool= new();
   
    private IChannel? _currentChannel;

    private const int Limit = 20;

    private static readonly SemaphoreSlim SemaphoreSlim = new(Limit, Limit);


    public async ValueTask<IChannel> GetChannelAsync()
    {
        await SemaphoreSlim.WaitAsync();
        
        if (ChannelPool.TryTake(out _currentChannel) && _currentChannel is { IsOpen: true })
            return _currentChannel;
        
        _currentChannel=await connection.CreateChannelAsync();

        return _currentChannel;
    }
    
    public void Dispose()
    {
        
        if(_currentChannel is {IsOpen:true})
            ChannelPool.Add(_currentChannel);

        SemaphoreSlim.Release();
    }
}