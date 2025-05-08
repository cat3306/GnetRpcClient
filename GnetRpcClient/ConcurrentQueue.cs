using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
namespace GnetRpcClient
{
    public class ConcurrentItemQueue<T>
    {
        private readonly BlockingCollection<T> _queue;
        public ConcurrentItemQueue(int cap = 100000)
        {
            _queue = new BlockingCollection<T>(cap);
        }
        public void Enqueue(T item)
        {
            _queue.Add(item);
        }
        public bool TryDequeue(out T? item)
        {
            return _queue.TryTake(out item);
        }

        public T Dequeue()
        {
            return _queue.Take();
        }
        public int Count => _queue.Count;

        public void Clear()
        {

        }
    }
}