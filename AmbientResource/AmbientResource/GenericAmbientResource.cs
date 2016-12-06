using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Timers;

namespace Nicolai.Resources
{
    public abstract class GenericAmbientResource<TSelf, TResource> : IDisposable where TResource : IDisposable
        where TSelf : GenericAmbientResource<TSelf, TResource>
    {
        private static readonly object currentLock = new object();
        private static TSelf current;
        private readonly Dictionary<string, int> referenceCounts = new Dictionary<string, int>();

        private readonly object referenceCountsLock = new object();

        protected TResource Resource;

        private Timer timer;

        public static Func<TResource> ResourceFactory { get; set; }

        public static long ResourceTimeoutMillis { get; set; } = 30000;

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
                        }
                        current.AddReferenceCount(GetCallerKey());
                    }
                    return current;
                }
                throw new ArgumentNullException(nameof(ResourceFactory),
                    nameof(ResourceFactory) + " must be initialized before calling " + nameof(Current));
            }
        }

        public void Dispose()
        {
            RemoveReferenceCount(GetCallerKey());
            lock (this.referenceCountsLock)
            {
                if (this.referenceCounts.Count == 0)
                {
                    // Stop the timeout timer
                    this.timer?.Stop();
                    this.Resource.Dispose();
                }
            }
        }

        private void AddReferenceCount(string caller)
        {
            lock (this.referenceCountsLock)
            {
                if (!this.referenceCounts.ContainsKey(caller))
                {
                    this.referenceCounts[caller] = 0;
                }
                this.referenceCounts[caller]++;
            }

            // Restart timer
            this.timer?.Stop();
            this.timer = new Timer {AutoReset = false, Interval = ResourceTimeoutMillis};
            this.timer.Elapsed += TimeoutTimerElapsed;
            this.timer.Start();
        }

        private void TimeoutTimerElapsed(object sender, ElapsedEventArgs e)
        {
            lock (this.referenceCountsLock)
            {
                if (this.referenceCounts.Count > 0)
                {
                    this.referenceCounts.Clear();
                    this.Resource.Dispose();
                }
            }
        }

        private void RemoveReferenceCount(string caller)
        {
            lock (this.referenceCountsLock)
            {
                if (this.referenceCounts.ContainsKey(caller))
                {
                    this.referenceCounts[caller]--;

                    if (this.referenceCounts[caller] == 0)
                    {
                        this.referenceCounts.Remove(caller);
                    }
                }
            }
        }

        private static string GetCallerKey()
        {
            var stackTrace = new StackTrace();
            var methodBase = stackTrace.GetFrame(2).GetMethod();
            return methodBase.Name;
        }
    }
}