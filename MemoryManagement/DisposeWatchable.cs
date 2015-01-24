using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemoryManagement
{
    public interface IDisposeWatchable : IDisposable
    {
        int OnDispose(Action action);
        void OffDispose(int id);
        bool IsDisposed();

    }

    public abstract class DisposeWatchable : IDisposeWatchable
    {
        private int watcherId = 0;
        private Dictionary<int, Action> Watchers = new Dictionary<int, Action>();

        public int OnDispose(Action action)
        {
            int curWatcherId = watcherId++;

            Watchers.Add(curWatcherId, action);

            return curWatcherId;
        }

        public void OffDispose(int id)
        {
            if (!Watchers.ContainsKey(id)) throw new KeyNotFoundException();
            Watchers.Remove(id);
        }

        public bool IsDisposed()
        {
            return disposed;
        }

        //http://msdn.microsoft.com/en-us/library/fs2xkftw(v=vs.110).aspx
        protected bool disposed { get; private set; }
        public virtual void Dispose()
        {
            if(IsDisposed()) return;
            disposed = true;
            Watchers.ForEach(x => x.Value());
            Watchers.Clear();
        }

        ~DisposeWatchable()
        {
            Dispose();
        }
    }

    public sealed class ResourceGuard : DisposeWatchable
    {
        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
