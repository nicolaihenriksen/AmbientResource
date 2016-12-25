using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nicolai.Resources
{
    public abstract class GenericAmbientResource<TSelf, TResource> : IDisposable where TResource : IDisposable
        where TSelf : GenericAmbientResource<TSelf, TResource>
    {
        private static readonly object currentLock = new object();
        private static TSelf current;

        public static int ActiveReferences { get; private set; }
        private readonly object referenceCountsLock = new object();

        protected TResource Resource;

        private Timer timer = new Timer();

        public static Func<TResource> ResourceFactory { get; set; }

        public static int ResourceTimeoutMillis { get; set; } = 30000;

        public static TSelf Current
        {
            get
            {
                if (ResourceFactory != null)
                {
                    lock (currentLock)
                    {
                        if (current == null)
                        {
                            current = Activator.CreateInstance<TSelf>();
                            current.Resource = ResourceFactory();
                            current.timer.TimerElapsed += current.TimeoutTimerElapsed;
                        }
                        current.AddReferenceCount();
                    }
                    return current;
                }
                throw new ArgumentNullException(nameof(ResourceFactory),
                    nameof(ResourceFactory) + " must be initialized before calling " + nameof(Current));
            }
        }

        public void Dispose()
        {
            lock (this.referenceCountsLock)
            {
                if (RemoveReferenceCount() && current != null)
                {
                    // Stop the timeout timer
                    this.timer?.Stop();
                    this.Resource.Dispose();
                    this.Resource = default(TResource);
                    current = null;
                }
            }
        }

        private void AddReferenceCount()
        {
            lock (this.referenceCountsLock)
            {
                ActiveReferences++;

                // Restart timer
                this.timer?.Stop();
                this.timer.Delay = ResourceTimeoutMillis;
                this.timer.Start();
            }
        }

        private void TimeoutTimerElapsed()
        {
            lock (this.referenceCountsLock)
            {
                if (ActiveReferences > 0)
                {
                    ActiveReferences = 1;
                    Dispose();
                }
            }
        }

        private bool RemoveReferenceCount()
        {
            lock (this.referenceCountsLock)
            {
                if (ActiveReferences > 0)
                {
                    ActiveReferences--;
                    return ActiveReferences == 0;
                }
                return false;
            }
        }

        internal static void Reset()
        {
            if (current != null)
            {
                lock (current.referenceCountsLock)
                {
                    ActiveReferences = 0;
                    current.timer?.Stop();
                    current.Resource = default(TResource);
                    current = null;
                }
            }
        }

    }

    internal class Timer
    {
        internal event Action TimerElapsed = delegate() {};
        internal int Delay { get; set; }
        private CancellationTokenSource cancellationToken;
        private bool started = false;        

        public void Start()
        {
            if (started)
                return;

            started = true;
            cancellationToken = new CancellationTokenSource();
            Task.Run(() => RunAsync(), cancellationToken.Token);
        }

        public void Stop()
        {
            cancellationToken?.Cancel();
            started = false;
        }

        private async Task RunAsync()
        {
            try
            {
                if (Delay > 0)
                {
                    await Task.Delay(Delay);
                    TimerElapsed.Invoke();
                }
            }
            catch (TaskCanceledException)
            { }
        }
    }

}