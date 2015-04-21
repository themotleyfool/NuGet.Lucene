using System;
using Autofac;
using Moq;
using NuGet.Lucene.Web.Controllers;
using NuGet.Lucene.Web.Models;
using NuGet.Lucene.Web.Symbols;
using NUnit.Framework;

namespace NuGet.Lucene.Web.Tests
{
    [TestFixture]
    public class NuGetWebApiModuleTests
    {
        private TestableNuGetWebApiModule module;
        private IContainer container;

        [Test]
        public void DisposeConfigurator()
        {
            BuildContainer();

            container.Dispose();

            module.Configurator.Verify(c => c.Dispose(), Times.Once);
        }

        [Test]
        public void DisposeUserStore()
        {
            BuildContainer();

            container.Dispose();

            module.UserStore.Verify(c => c.Dispose(), Times.Once);
        }

        [Test]
        public void RegistersRepository()
        {
            BuildContainer();

            var result = container.Resolve<ILucenePackageRepository>();

            Assert.That(result, Is.SameAs(module.Repository.Object));
        }

        [Test]
        public void RegistersControllersWithAutowire()
        {
            BuildContainer();

            var result = container.Resolve<IndexingController>();

            Assert.That(result.Repository, Is.SameAs(module.Repository.Object));
        }

        [Test]
        [TestCase(typeof(IMirroringPackageRepository))]
        [TestCase(typeof(NuGetWebApiRouteMapper))]
        [TestCase(typeof(IUserStore))]
        [TestCase(typeof(ISymbolSource))]
        [TestCase(typeof(SymbolTools))]
        public void RegistersType(Type type)
        {
            BuildContainer();
            TestDelegate call = () => container.Resolve(type);
            Assert.That(call, Throws.Nothing);
        }

        private void BuildContainer()
        {
            var builder = new ContainerBuilder();
            module = new TestableNuGetWebApiModule();
            builder.RegisterModule(module);
            container = builder.Build();
        }

        class TestableNuGetWebApiModule : NuGetWebApiModule
        {
            public Mock<ILuceneRepositoryConfigurator> Configurator { get; private set; }
            public Mock<ILucenePackageRepository> Repository { get; private set; }
            public Mock<IUserStore> UserStore { get; private set; }

            protected override ILuceneRepositoryConfigurator InitializeRepositoryConfigurator(INuGetWebApiSettings settings)
            {
                Configurator = new Mock<ILuceneRepositoryConfigurator>();
                Repository = new Mock<ILucenePackageRepository>();
                Configurator.Setup(c => c.Repository).Returns(Repository.Object);
                return Configurator.Object;
            }

            protected override IUserStore InitializeUserStore(INuGetWebApiSettings settings)
            {
                UserStore = new Mock<IUserStore>();
                return UserStore.Object;
            }
        }
    }
}