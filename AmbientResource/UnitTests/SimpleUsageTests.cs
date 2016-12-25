using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nicolai.Resources;
using System.Threading.Tasks;
using System.Threading;

namespace UnitTests
{
    [TestClass]
    public class SimpleUsageTests
    {
        [TestInitialize]
        public void Initialize()
        {
            MyAmbientResource.ResourceFactory = () => new MyResource();
        }

        [TestCleanup]
        public void Cleanup()
        {
            MyAmbientResource.Reset();
        }

        [TestMethod]
        public void Using_SingleScope_Success()
        {
            // Arrange
            MyResource actualResource = null;

            // Act
            using (var res = MyAmbientResource.Current)
            {
                actualResource = res.ActualResource;
            }

            // Assert
            Assert.IsNotNull(actualResource);
            Assert.AreEqual(1, actualResource.DisposeCount);
            Assert.AreEqual(0, MyAmbientResource.ActiveReferences);
        }

        [TestMethod]
        public void Using_NestedScopeSameMethod_Success()
        {
            // Arrange
            MyResource actualResource = null;

            // Act
            using (var res = MyAmbientResource.Current)
            {
                actualResource = res.ActualResource;

                using (var res2 = MyAmbientResource.Current)
                {
                    // Dispose count should NOT increase after this scope exits
                }
            }

            // Assert
            Assert.IsNotNull(actualResource);
            Assert.AreEqual(1, actualResource.DisposeCount);
            Assert.AreEqual(0, MyAmbientResource.ActiveReferences);
        }

        [TestMethod]
        public void Using_NestedScope_Success()
        {
            // Arrange
            MyResource actualResource = null;

            // Act
            using (var res = MyAmbientResource.Current)
            {
                actualResource = res.ActualResource;

                UseResource();
            }

            // Assert
            Assert.IsNotNull(actualResource);
            Assert.AreEqual(1, actualResource.DisposeCount);
            Assert.AreEqual(0, MyAmbientResource.ActiveReferences);
        }

        private void UseResource()
        {
            using (var res2 = MyAmbientResource.Current)
            {
                // Dispose count should NOT increase after this scope exits (if called from active scope)
            }
        }

        [TestMethod]
        public void Using_SingleScopeAndCallToCurrent_Success()
        {
            // Arrange
            MyResource actualResource = null;

            // Act
            using (var res = MyAmbientResource.Current)
            {
                actualResource = res.ActualResource;

                var res2 = MyAmbientResource.Current;

                res2.Dispose();
            }

            // Assert
            Assert.IsNotNull(actualResource);
            Assert.AreEqual(1, actualResource.DisposeCount);
            Assert.AreEqual(0, MyAmbientResource.ActiveReferences);
        }

        [TestMethod]
        public async Task Reference_Timeout_Success()
        {
            var timeout = MyAmbientResource.ResourceTimeoutMillis;
            try
            {
                // Arrange
                MyResource actualResource = null;
                MyAmbientResource.ResourceTimeoutMillis = 500;

                // Act
                var res = MyAmbientResource.Current;
                actualResource = res.ActualResource;
                await Task.Delay(600);

                // Assert
                Assert.IsNotNull(actualResource);
                Assert.AreEqual(1, actualResource.DisposeCount);
                Assert.AreEqual(0, MyAmbientResource.ActiveReferences);
            }
            finally
            {
                MyAmbientResource.ResourceTimeoutMillis = timeout;
            }
        }

        [TestMethod]
        public async Task Reference_NotTimedOutAndThenTimeout_Success()
        {
            var timeout = MyAmbientResource.ResourceTimeoutMillis;
            try
            {
                // Arrange
                MyResource actualResource = null;
                MyAmbientResource.ResourceTimeoutMillis = 500;

                // Act
                var res = MyAmbientResource.Current;
                actualResource = res.ActualResource;
                await Task.Delay(200);

                // Assert (not timed out)
                Assert.IsNotNull(actualResource);
                Assert.AreEqual(0, actualResource.DisposeCount);
                Assert.AreEqual(1, MyAmbientResource.ActiveReferences);

                await Task.Delay(400);

                // Assert (timed out)
                Assert.IsNotNull(actualResource);
                Assert.AreEqual(1, actualResource.DisposeCount);
                Assert.AreEqual(0, MyAmbientResource.ActiveReferences);
            }
            finally
            {
                MyAmbientResource.ResourceTimeoutMillis = timeout;
            }
        }

        [TestMethod]
        public async Task Reference_NoAdditionalTimeoutDispose_Success()
        {
            var timeout = MyAmbientResource.ResourceTimeoutMillis;
            try
            {
                // Arrange
                MyResource actualResource = null;
                MyAmbientResource.ResourceTimeoutMillis = 500;

                // Act
                var res = MyAmbientResource.Current;
                actualResource = res.ActualResource;
                await Task.Delay(200);
                res.Dispose();

                // Assert
                Assert.IsNotNull(actualResource);
                Assert.AreEqual(1, actualResource.DisposeCount);
                Assert.AreEqual(0, MyAmbientResource.ActiveReferences);

                await Task.Delay(400);

                // Assert (no changes)
                Assert.IsNotNull(actualResource);
                Assert.AreEqual(1, actualResource.DisposeCount);
                Assert.AreEqual(0, MyAmbientResource.ActiveReferences);
            }
            finally
            {
                MyAmbientResource.ResourceTimeoutMillis = timeout;
            }
        }

        [TestMethod]
        public async Task Reference_TimeoutDisabled_Success()
        {
            var timeout = MyAmbientResource.ResourceTimeoutMillis;
            try
            {
                // Arrange
                MyResource actualResource = null;
                MyAmbientResource.ResourceTimeoutMillis = 0;

                // Act
                var res = MyAmbientResource.Current;
                actualResource = res.ActualResource;
                await Task.Delay(200);

                // Assert
                Assert.IsNotNull(actualResource);
                Assert.AreEqual(0, actualResource.DisposeCount);
                Assert.AreEqual(1, MyAmbientResource.ActiveReferences);

                // Act
                res.Dispose();

                // Assert
                Assert.IsNotNull(actualResource);
                Assert.AreEqual(1, actualResource.DisposeCount);
                Assert.AreEqual(0, MyAmbientResource.ActiveReferences);
            }
            finally
            {
                MyAmbientResource.ResourceTimeoutMillis = timeout;
            }
        }

        [TestMethod]
        public async Task Using_MultipleThreads_Success()
        {
            // Arrange
            MyResource actualResource1 = null;
            MyResource actualResource2 = null;
            var thread1 = Task.Run(() =>
            {
                using (var res = MyAmbientResource.Current)
                {
                    actualResource1 = res.ActualResource;
                    Thread.Sleep(500);
                }
            });
            var thread2 = Task.Run(() =>
            {
                using (var res = MyAmbientResource.Current)
                {
                    actualResource2 = res.ActualResource;
                    Thread.Sleep(500);
                }
            });

            // Act
            await Task.WhenAll(thread1, thread2);

            // Assert
            Assert.IsNotNull(actualResource1);
            Assert.IsNotNull(actualResource2);
            Assert.AreEqual(actualResource1, actualResource2);
            Assert.AreEqual(1, actualResource1.DisposeCount);
            Assert.AreEqual(0, MyAmbientResource.ActiveReferences);
        }
    }

    class MyAmbientResource : GenericAmbientResource<MyAmbientResource, MyResource>
    {
        public MyResource ActualResource => Resource;
    }

    class MyResource : IDisposable
    {
        public int DisposeCount { get; private set; }

        public virtual void Dispose()
        {
            lock (this)
            {
                DisposeCount++;
            }
        }
    }
}
