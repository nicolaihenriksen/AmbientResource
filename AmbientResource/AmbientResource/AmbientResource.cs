using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Nicolai.Resources
{
    public class AmbientResource : IDisposable
    {

        public static Func<IDisposable> ResourceFactory { get; set; }

        private static readonly object CurrentLock = new object();
        private static AmbientResource _current;

        public static AmbientResource Current
        {
            get
            {
                if (ResourceFactory != null)
                {
                    lock (CurrentLock)
                    {
                        if (_current == null)
                        {
                            _current = new AmbientResource { Resource = ResourceFactory() };
                        }
                    }

                    _current.AddReferenceCount(GetCallerKey());
                    return _current;
                }
                throw new ArgumentNullException(nameof(ResourceFactory), nameof(ResourceFactory) + " must be initialized before calling " + nameof(Current));
            }
        }

        private readonly object _referenceCountsLock = new object();
        private readonly Dictionary<string, int> _referenceCounts = new Dictionary<string, int>();

        protected IDisposable Resource;

        protected AmbientResource() { }

        private void AddReferenceCount(string caller)
        {
            lock (_referenceCountsLock)
            {
                if (!_referenceCounts.ContainsKey(caller))
                {
                    _referenceCounts[caller] = 0;
                }
                _referenceCounts[caller]++;
            }
        }

        private void RemoveReferenceCount(string caller)
        {
            lock (_referenceCountsLock)
            {
                if (_referenceCounts.ContainsKey(caller))
                {
                    _referenceCounts[caller]--;

                    if (_referenceCounts[caller] == 0)
                    {
                        _referenceCounts.Remove(caller);
                    }
                }
            }
        }

        private static string GetCallerKey()
        {
            StackTrace stackTrace = new StackTrace();
            MethodBase methodBase = stackTrace.GetFrame(2).GetMethod();
            return methodBase.Name;
        }

        public void Dispose()
        {
            RemoveReferenceCount(GetCallerKey());
            if (_referenceCounts.Count == 0)
            {
                Resource.Dispose();
            }
        }
    }
}
