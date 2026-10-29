using Microsoft.Extensions.Caching.Memory;
namespace SmartQueueSystemGUI.Services;
public sealed class AssistantThrottle : IDisposable
{
    private readonly MemoryCache _cache=new(new MemoryCacheOptions {SizeLimit=10000});
    private readonly object _gate=new();
    public bool Allow(string key,int limit,TimeSpan window) {
        lock(_gate) {
            if(!_cache.TryGetValue<Counter>(key,out var entry)||entry==null)entry=new Counter{Expires=DateTimeOffset.UtcNow.Add(window)};
            if(entry.Count>=limit)return false;entry.Count++;
            _cache.Set(key,entry,new MemoryCacheEntryOptions{AbsoluteExpiration=entry.Expires,Size=1});return true;
        }
    }
    private class Counter {public int Count;public DateTimeOffset Expires;}
    public void Dispose()=>_cache.Dispose();
}
