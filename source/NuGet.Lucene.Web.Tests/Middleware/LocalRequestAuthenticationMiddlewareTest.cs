using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Linq;
using Lucene.Net.Store;
using Lucene.Net.Util;
using NuGet.Lucene.Web.Middleware;
using NUnit.Framework;

namespace NuGet.Lucene.Web.Tests.Middleware
{
    [TestFixture]
    public class LocalRequestAuthenticationMiddlewareTest : AuthenticationMiddlewareTestBase
    {
        private LocalRequestAuthenticationMiddleware middleware;

        [SetUp]
        public void SetUp()
        {
            IUserStore store = new UserStore(new LuceneDataProvider(new RAMDirectory(), Version.LUCENE_30));
            middleware = new LocalRequestAuthenticationMiddleware(next) { Store = store };
        }

        [Test]
        public async Task Authenticated_DoesNothing()
        {
            request.User = user;

            await middleware.Invoke(context);

            Assert.That(request.User, Is.SameAs(user), "request.User");
        }

        [Test]
        public async Task RequestIsLocal_GrantsRoles()
        {
            requestMock.Setup(m => m.Get<bool>("server.IsLocal")).Returns(true);
            await middleware.Invoke(context);

            Assert.That(request.User.Identity.IsAuthenticated, Is.True, "IsAuthenticated");
            Assert.That(RoleNames.All.All(r => request.User.IsInRole(r)), Is.True, "Is in all roles in RoleNames.All");
        }

        [Test]
        public async Task RequestNotLocal_DoesNothing()
        {
            await middleware.Invoke(context);

            Assert.That(request.User, Is.Null, "request.User");
        }
    }
}