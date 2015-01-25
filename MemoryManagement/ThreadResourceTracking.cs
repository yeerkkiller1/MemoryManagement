using Extensions;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MemoryManagement
{
    public class ThreadGuard : DisposeWatchable
    {
        public ThreadGuard()
        {
            ThreadResourceTracking.AttachDispose(this);
        }
        public override void Dispose()
        {
            base.Dispose();
        }
    }

    public static class ThreadResourceTracking
    {
        public static bool NoThreadDisposing = false;

        private static List<IResources> threadResources = new List<IResources>();

        [ThreadStatic]
        private static DisposeWatchable threadDisposeInstance;

        public static void SetupForTests()
        {
            Ensure.IsInTest();
            threadDisposeInstance = null;
        }

        public static void AttachDispose(DisposeWatchable disposeWatchable)
        {
            if(threadDisposeInstance != null)
            {
                throw new Exception("You are clobbering the previous DisposeWatchable, this should be some code that is very controlled"
                + "within a using clause that is around the entirety of the thread's code. Try doing this, an using a producer consumer structure to pass it data");
            }

            threadDisposeInstance = disposeWatchable;
            threadDisposeInstance.OnDispose(() =>
            {
                if(NoThreadDisposing)
                {
                    throw new Exception("Thread disposed when no threads were supposed to dispose!");
                }
                ReleaseAll();
                //Eh... okay, we will may it reusable, just for the tests :D
                threadDisposeInstance = null;
            });
        }

        private static void ReleaseAll()
        {
            //Hmm... so we never release threadResources?
            threadResources.ForEach(x => x.DisposeCurrentThreadResources());
        }

        private static void TrackResources(IResources resources)
        {
            threadResources.Add(resources);
        }

        private static void EnsureIsDisposeAllowed()
        {
            if(threadDisposeInstance == null)
            {
                throw new Exception("Before you attach resources to a thread it must call ThreadResourceTracking.AttachDispose"
                    + " so you are sure to dispose the resources. This prevents you from accidently spawning threads that then leak resources.");
            }
        }

        interface IResources
        {
            void DisposeCurrentThreadResources();
        }

        //Create this once, and then threads can call AttachDispose and then they can
        //  conveniently kill threads and free all their resources.
        //WE LEAK THIS! So make only store static instances...
        public class Resources<T> : IResources
        {
            readonly Action<T> resourceDispose;

            //Hmm... maybe this whole thing should be ThreadStatic... but
            //  I am not sure if I am going to want to go through all
            //  the resources for all threads easily... so I am not
            //  going to expose that interface.
            [ThreadStatic]
            static SingleThreadResources internal_threadResources;

            static SingleThreadResources threadResources
            {
                get
                {
                    internal_threadResources = internal_threadResources ?? new SingleThreadResources();
                    return internal_threadResources;
                }
            }

            private class SingleThreadResources
            {
                List<T> resources = new List<T>();
                public void AddResource(T resource)
                {
                    Ensure.IsSingleThreaded();
                    resources.Add(resource);
                }
                public void RemoveResource(T resource)
                {
                    //This means we are being called from the wrong Thread...
                    //  probably okay, they should being doing cleanup after/before
                    //  calling this, so they are not leaking anything.
                    //if (!resources.Contains(resource))
                    resources.Remove(resource);
                }
                public void FreeResources(Action<T> dispose)
                {
                    resources.ForEach(dispose);
                    resources.Clear();
                }
            }

            public Resources(Action<T> resourceDispose)
            {
                this.resourceDispose = resourceDispose;
                ThreadResourceTracking.TrackResources(this);
            }

            public void TrackResource(T obj)
            {
                ThreadResourceTracking.EnsureIsDisposeAllowed();
                threadResources.AddResource(obj);
            }
            public void UntrackResource(T obj)
            {
                threadResources.RemoveResource(obj);
            }

            //This is like a normal dispose, so you can only call it once per thread
            //  and after you dispose you can't track more resources...
            public void DisposeCurrentThreadResources()
            {
                ThreadResourceTracking.EnsureIsDisposeAllowed();

                threadResources.FreeResources(resourceDispose);
            }
        }



    }
}
