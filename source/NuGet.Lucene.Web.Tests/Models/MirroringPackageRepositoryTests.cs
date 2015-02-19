using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Lucene.Web.Models;
using NuGet.Lucene.Web.Util;
using NUnit.Framework;

namespace NuGet.Lucene.Web.Tests.Models
{
    [TestFixture]
    public class MirroringPackageRepositoryTests
    {
        private IPackage package1;
        private IPackage package2;
        private IPackage package3;
        private Mock<ICache> cache;
        private Mock<IPackageLookup> mirror;
        private Mock<IPackageLookup> origin;
        private MirroringPackageRepository repo;

        [SetUp]
        public void SetUp()
        {
            mirror = new Mock<IPackageLookup>();
            origin = new Mock<IPackageLookup>();
            cache = new Mock<ICache>();

            repo = new MirroringPackageRepository(mirror.Object, new[] { origin.Object }, cache.Object);

            package1 = new LucenePackage(_ => null) { Id = "FuTools", Version = new StrictSemanticVersion("1.0"), IsMirrored = true };
            package2 = new LucenePackage(_ => null) { Id = "FuTools", Version = new StrictSemanticVersion("2.0") };
            package3 = new LucenePackage(_ => null) { Id = "FuTools", Version = new StrictSemanticVersion("3.0") };
        }

        [Test]
        public void FindPackagesDistinct()
        {
            var copyOfPackage1 = new PackageSpec(package1.Id, package1.Version.ToString());

            mirror.Setup(r => r.FindPackagesById("FuTools")).Returns(new[] { package1 }).Verifiable();
            origin.Setup(r => r.FindPackagesById("FuTools")).Returns(new[] { copyOfPackage1, package2 }).Verifiable();

            var result = repo.FindPackagesById("FuTools");

            mirror.VerifyAll();
            origin.VerifyAll();

            Assert.That(result.ToList(), Is.EqualTo(new[] { package1, package2 }));
        }

        [Test]
        public void FindPackagesLooksInOriginWhenNoneInMirror()
        {
            mirror.Setup(r => r.FindPackagesById("FuTools")).Returns(new IPackage[0]).Verifiable();
            origin.Setup(r => r.FindPackagesById("FuTools")).Returns(new[] { package1, package2 }).Verifiable();

            var result = repo.FindPackagesById("FuTools");

            mirror.VerifyAll();
            origin.VerifyAll();

            Assert.That(result.ToList(), Is.EqualTo(new[] { package1, package2 }));
        }

        [Test]
        public void FindPackagesSkipsOriginOnLocalPackage()
        {
            ((LucenePackage)package1).IsMirrored = false;
            mirror.Setup(r => r.FindPackagesById("FuTools")).Returns(new[] { package1 }).Verifiable();

            var result = repo.FindPackagesById("FuTools");

            mirror.VerifyAll();

            Assert.That(result.ToList(), Is.EqualTo(new[] { package1 }));

            origin.Verify(r => r.FindPackagesById("FuTools"), Times.Never);
        }

        [Test]
        public void FindPackagesSkipsOriginWhenANonMirroredPackageIsPresent()
        {
            mirror.Setup(r => r.FindPackagesById("FuTools")).Returns(new[] { package1, package2 }).Verifiable();
            origin.Setup(r => r.FindPackagesById("FuTools")).Returns(new[] { package3 }).Verifiable();

            var result = repo.FindPackagesById("FuTools");

            mirror.VerifyAll();

            Assert.That(result.ToList(), Is.EqualTo(new[] { package1, package2 }));

            origin.Verify(r => r.FindPackagesById("FuTools"), Times.Never);
        }

        [Test]
        public void FindPackagesAlwaysGoesToOriginIfOverideToAlwaysCheckOrigin()
        {
            repo = new EagerMirroringPackageRepository(mirror.Object, new[] { origin.Object }, cache.Object);

            mirror.Setup(r => r.FindPackagesById("FuTools")).Returns(new[] { package1, package2 }).Verifiable();
            origin.Setup(r => r.FindPackagesById("FuTools")).Returns(new[] { package3 }).Verifiable();

            var result = repo.FindPackagesById("FuTools");

            mirror.VerifyAll();

            Assert.That(result.ToList(), Is.EqualTo(new[] { package1, package2, package3 }));

            origin.Verify(r => r.FindPackagesById("FuTools"), Times.Once);
        }

        [Test]
        public void FindPackagesHandlesOriginException()
        {
            mirror.Setup(r => r.FindPackagesById("FuTools")).Returns(new[] { package1 }).Verifiable();
            origin.Setup(r => r.FindPackagesById("FuTools")).Throws<Exception>().Verifiable();

            IList<IPackage> result = null;
            TestDelegate call = () => result = repo.FindPackagesById("FuTools").ToList();

            Assert.That(call, Throws.Nothing);
            Assert.That(result, Is.EqualTo(new[] { package1 }));

            mirror.VerifyAll();
            origin.VerifyAll();
        }

        [Test]
        public void FindPackageInMirror()
        {
            mirror.Setup(r => r.FindPackage(package1.Id, package1.Version)).Returns(package1).Verifiable();

            var result = repo.FindPackage(package1.Id, package1.Version);

            Assert.That(result, Is.SameAs(package1));

            mirror.VerifyAll();
            origin.Verify(r => r.FindPackage(It.IsAny<string>(), It.IsAny<SemanticVersion>()), Times.Never());
        }

        [Test]
        public void PackageNotFound()
        {
            mirror.Setup(r => r.FindPackage(package1.Id, package1.Version)).Returns((IPackage)null).Verifiable();
            origin.Setup(r => r.FindPackage(package1.Id, package1.Version)).Returns((IPackage)null).Verifiable();

            var result = repo.FindPackage(package1.Id, package1.Version);

            Assert.That(result, Is.Null);

            mirror.VerifyAll();
            origin.VerifyAll();
        }

        [Test]
        public void PackageInOriginAddedToMirror()
        {
            var addedPackage = (IPackage)null;

            mirror.Setup(r => r.FindPackage(package1.Id, package1.Version)).Returns(() => addedPackage);
            origin.Setup(r => r.FindPackage(package1.Id, package1.Version)).Returns(package1).Verifiable();
            mirror.Setup(r => r.AddPackage(package1)).Callback<IPackage>(pkg => addedPackage = new PackageSpec(pkg.Id, pkg.Version.ToString()));

            var result = repo.FindPackage(package1.Id, package1.Version);

            Assert.That(result, Is.SameAs(addedPackage));

            mirror.VerifyAll();
            origin.VerifyAll();
        }

        [Test]
        public void FindPackageInOriginHandlesExceptions()
        {
            origin.Setup(r => r.FindPackage(package1.Id, package1.Version)).Throws<Exception>();

            IPackage result = null;

            TestDelegate call = () => result = repo.FindPackage(package1.Id, package1.Version);

            Assert.That(call, Throws.Nothing);
            Assert.That(result, Is.Null);
        }
    }
}
