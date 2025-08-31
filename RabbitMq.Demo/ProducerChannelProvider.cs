using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace RabbitMq.Demo;

public class ProducerChannelProvider(IConnection connection):IDisposable
{
    private static readonly ConcurrentBag<IChannel> ChannelPool = [];
    private IChannel? _currentChannel;
    private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);


    public async Task<IChannel> GetChannelAsync()
    {
        await Semaphore.WaitAsync();
        
        Console.WriteLine($"Number of channels in pool: {ChannelPool.Count}");
        Console.WriteLine($"Number of active channels: {ChannelPool.Count(c=>c.IsOpen)}");
        
        if (ChannelPool.TryTake(out _currentChannel) && _currentChannel is {IsOpen:true})
            return _currentChannel;
        
        _currentChannel=await connection.CreateChannelAsync();
        
        return _currentChannel;
    }

    public void Dispose()
    {
        Semaphore.Release();
        
        if (_currentChannel is null)
            return;
        
        if(_currentChannel.IsClosed)
            return;
        
        ChannelPool.Add(_currentChannel);
    }
}