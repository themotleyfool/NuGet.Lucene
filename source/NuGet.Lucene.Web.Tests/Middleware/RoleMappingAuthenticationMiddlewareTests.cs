using System.Collections.Specialized;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Lucene.Net.Linq;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Moq;
using NuGet.Lucene.Web.Authentication;
using NuGet.Lucene.Web.Middleware;
using NUnit.Framework;

namespace NuGet.Lucene.Web.Tests.Middleware
{
    [TestFixture]
    public class RoleMappingAuthenticationMiddlewareTests : AuthenticationMiddlewareTestBase
    {
        private RoleMappingAuthenticationMiddleware middleware;
        private IUserStore store;
        private NameValueCollection mappings;
        private ApiUserPrincipal domainAdminUser;

        [SetUp]
        public void SetUp()
        {
            store = new UserStore(new LuceneDataProvider(new RAMDirectory(), Version.LUCENE_30));
            mappings = new NameValueCollection();
            middleware = new RoleMappingAuthenticationMiddleware(next) {Store = store, Settings = new NuGetWebApiSettings(NuGetWebApiSettings.DefaultAppSettingPrefix, new NameValueCollection(), mappings)};
            domainAdminUser = new ApiUserPrincipal(new GenericIdentity("T-Rex"), new [] {"Domain Administrators"});
        }

        [Test]
        public async Task NotAuthenticated_DoesNothing()
        {
            await middleware.Invoke(context);

            Assert.That(store.All.Count(), Is.EqualTo(0), "store.All.Count()");
        }

        [Test]
        public async Task NotAuthenticated_InvokesNext()
        {
            await middleware.Invoke(context);

            nextMock.Verify(m => m.Invoke(context), Times.Once);
        }

        [Test]
        public async Task Authenticated_NotInUserStore_CreatesUserWithApiKey()
        {
            request.User = domainAdminUser;

            await middleware.Invoke(context);

            var apiUser = store.FindByUsername(domainAdminUser.Identity.Name);
            Assert.That(apiUser, Is.Not.Null, "store.FindByUsername()");
            Assert.That(apiUser.Key, Is.Not.Empty, "apiUser.Key");
        }

        [Test]
        public async Task Authenticated_AlreadyInUserStore_LeavesApiKey()
        {
            request.User = domainAdminUser;
            store.Add(new ApiUser { Username = domainAdminUser.Identity.Name, Key = "idemponent", Roles = new[] {RoleNames.AccountAdministrator} });

            await middleware.Invoke(context);

            var apiUser = store.FindByUsername(domainAdminUser.Identity.Name);
            Assert.That(apiUser.Key, Is.EqualTo("idemponent"), "apiUser.Key");
        }

        [Test]
        public async Task Authenticated_GrantsMappedRole()
        {
            request.User = domainAdminUser;
            mappings.Add(RoleNames.AccountAdministrator, "Domain Administrators");
            await middleware.Invoke(context);

            Assert.That(request.User.IsInRole(RoleNames.AccountAdministrator), Is.True, "request.User.IsInRole(RoleNames.AccountAdministrator)");
        }

        [Test]
        public async Task Authenticated_GrantsRolesFromUserStore()
        {
            request.User = domainAdminUser;
            store.Add(new ApiUser { Username = domainAdminUser.Identity.Name, Key = "idemponent", Roles = new[] { RoleNames.AccountAdministrator } });
            mappings.Add(RoleNames.AccountAdministrator, "Domain Administrators");

            await middleware.Invoke(context);

            Assert.That(request.User.IsInRole(RoleNames.AccountAdministrator), Is.True, "request.User.IsInRole(RoleNames.AccountAdministrator)");
        }

        [Test]
        public async Task Authenticated_GrantsMappedRole_PreservesIdentity()
        {
            request.User = domainAdminUser;
            mappings.Add(RoleNames.AccountAdministrator, "Domain Administrators");
            await middleware.Invoke(context);

            Assert.That(request.User.Identity, Is.SameAs(domainAdminUser.Identity), "request.User.Identity");
        }
    }
}
