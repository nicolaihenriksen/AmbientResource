using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nicolai.Resources;

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
        }

        private void UseResource()
        {
            using (var res2 = MyAmbientResource.Current)
            {
                // Dispose count should NOT increase after this scope exits (if called from active scope)
            }
        }
    }

    class MyAmbientResource : GenericAmbientResource<MyAmbientResource, MyResource>
    {
        public MyResource ActualResource => Resource;
    }

    class MyResource : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
