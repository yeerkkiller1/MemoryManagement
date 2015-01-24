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
    //We need this so we can tell the different between a Job which is an Action
    //  and our invoke actions.
    public delegate void OurInvoke();

    public delegate void VariableSetup<in Context>(Context context);

    // We have our own thread which we should do all of our work on.
    // We also work with ThreadResourceTracking, so as long
    //      as all of our work is done on our thread,
    //      we shouldn't leak any thread tracked resources.
    // YOU MUST CALL InterfaceReady when you are ready for your CTOR
    //      to exit... if you don't ObservableSetup will never be called
    //      and you will never process any jobs... 
    public abstract class ThreadedWorker<Job> : DisposeWatchable
    {
        Action<Exception> OnJobThrow = null;

        Thread handlerThread;
        CancellationTokenSource tokenSource = new CancellationTokenSource();

        ManualResetEvent interfaceReady = new ManualResetEvent(false);
        ManualResetEvent threadSetup = new ManualResetEvent(false);

        //Either an Action (call it), or a string (a message)
        BlockingCollection<object> jobsToHandle = new BlockingCollection<object>();

        public ThreadedWorker()
        {
            interfaceReadyThread = Thread.CurrentThread;

            OnDispose(() =>
            {
                threadSetup.WaitOne();
            });

            handlerThread = new Thread(JobLoop);
            handlerThread.Start();
        }

        public void SetJobThrowHandler(Action<Exception> onThrow)
        {
            EnsureWorkerThread();
            this.OnJobThrow = onThrow;
        }

        Thread interfaceReadyThread;
        bool interfaceReadyCalled = false;
        public void InterfaceReady()
        {
            Ensure.IsOnThread(interfaceReadyThread, "InterfaceReady should be called from your CONSTRUCTOR (and on that thread)!");

            if (interfaceReadyCalled)
            {
                throw new Exception("InterfaceReady was already called, call this in your CONSTRUCTOR!");
            }

            interfaceReadyCalled = true;
            interfaceReady.Set();

            //Uncommenting out this will slow down initialize, but making
            //  it a lot safer.
            //threadSetup.WaitOne();
        }

        protected virtual void ObservSetup() { }

        public void Invoke(Action action)
        {
            if(Thread.CurrentThread == handlerThread)
            {
                //Eh... does change the excecution order... but w/e
                action();
                return;
            }
            jobsToHandle.Add(new OurInvoke(action));
        }

        public void EnsureWorkerThread()
        {
            Ensure.IsOnThread(handlerThread, "Called function on wrong thread use Invoke instead");
        }

        public void AddJob(Job job)
        {
            jobsToHandle.Add(job);
        }

        protected abstract void DoJob(Job job);
        private void JobLoop()
        {
            using(var holder = new ResourceGuard())
            {
                ThreadResourceTracking.AttachDispose(holder);

                interfaceReady.WaitOne();

                //TODO: We should add a massive debug delay here to make sure the interface
                //  is actually ready, and nothing is really waiting on ObservableSetup

                ObservSetup();

                threadSetup.Set();

                var jobs = jobsToHandle.GetConsumingEnumerable(tokenSource.Token);

                try
                {
                    foreach (object job in jobs)
                    {
                        if (OnJobThrow == null)
                        {
                            DoJobInner(job);
                        }
                        else
                        {
                            try
                            {
                                DoJobInner(job);
                            }
                            catch(Exception e)
                            {
                                OnJobThrow(e);
                            }
                        }
                    }
                }
                    //This is how we are supposed to have cancellation?
                catch (OperationCanceledException) { }
            }
        }
        private void DoJobInner(object job)
        {
            if (job is Job)
            {
                DoJob((Job)job);
            }
            else if (job is OurInvoke)
            {
                ((OurInvoke)job)();
            }
            else
            {
                throw new Exception("No idea how to handle type in WSConn handler queue: " + job.GetType());
            }
        }

        public override void Dispose()
        {
            if (IsDisposed()) return;

            tokenSource.Cancel(true);

            base.Dispose();
        }
    }
}
