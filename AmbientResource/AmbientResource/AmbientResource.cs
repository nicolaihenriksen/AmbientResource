using System;

namespace Nicolai.Resources
{
    public class AmbientResource : IDisposable
    {

        public static Func<IDisposable> ResourceFactory { get; set; }

        private static readonly object CurrentLock = new object();
        private static AmbientResource current;

        public static AmbientResource Current
        {
            get
            {
                if (ResourceFactory != null)
                {
                    lock (CurrentLock)
                    {
                        if (current == null)
                        {
                            current = new AmbientResource { Resource = ResourceFactory() };
                        }
                        current.AddReferenceCount();
                    }
                    return current;
                }
                throw new ArgumentNullException(nameof(ResourceFactory),
                    nameof(ResourceFactory) + " must be initialized before calling " + nameof(Current));
            }
        }

        public int ActiveReferences { get; private set; }
        private readonly object referenceCountsLock = new object();

        protected IDisposable Resource;

        protected AmbientResource() { }

        private void AddReferenceCount()
        {
            lock (referenceCountsLock)
            {
                ActiveReferences++;
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

        public void Dispose()
        {
            lock (this.referenceCountsLock)
            {
                if (RemoveReferenceCount() && current != null)
                {
                    this.Resource.Dispose();
                    this.Resource = null;
                    current = null;
                }
            }
        }
    }
}
