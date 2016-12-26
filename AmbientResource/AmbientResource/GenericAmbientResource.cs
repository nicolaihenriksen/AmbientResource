using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nicolai.Resources
{
    /// <summary>
    /// Generic ambient resource class intended to be subclassed with an actual implementation
    /// </summary>
    /// <typeparam name="TSelf">The type of the class deriving from this base class</typeparam>
    /// <typeparam name="TResource">The type of the resource which it can provide</typeparam>
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

        private static int resouceTimeoutMillis = 30000;

        /// <summary>
        /// Timeout in milliseconds. If a resource has not been disposed after it will be
        /// automatically disposed after this timeout. Set a value of 0 (or less) to disable.
        /// </summary>
        public static int ResourceTimeoutMillis
        {
            get
            {
                return resouceTimeoutMillis;
            }
            set
            {
                resouceTimeoutMillis = value;
                lock (currentLock)
                {
                    if (current != null && current.timer != null)
                    {
                        current.timer.Delay = value;
                    }
                }
            }
        }

        /// <summary>
        /// (Ambient) Reference to the current active instance of the resource
        /// </summary>
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

        /// <summary>
        /// Disposes the resource unless there are active references
        /// </summary>
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
                    if (Delay > 0)
                        TimerElapsed.Invoke();
                }
            }
            catch (TaskCanceledException)
            { }
        }
    }

}