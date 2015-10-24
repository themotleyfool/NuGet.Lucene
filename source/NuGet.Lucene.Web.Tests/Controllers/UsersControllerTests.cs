using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using Lucene.Net.Linq;
using Lucene.Net.Store;
using NuGet.Lucene.Web.Authentication;
using NuGet.Lucene.Web.Controllers;
using NuGet.Lucene.Web.Models;
using NUnit.Framework;
using Version = Lucene.Net.Util.Version;

namespace NuGet.Lucene.Web.Tests.Controllers
{
    [TestFixture]
    public class UsersControllerTests : ApiControllerTests<UsersController>
    {
        private IUserStore store;

        protected override UsersController CreateController()
        {
            var provider = new LuceneDataProvider(new RAMDirectory(), Version.LUCENE_30);
            store = new UserStore(provider);
            return new UsersController { Store = store };
        }

        public class ChangeApiKeyTests : UsersControllerTests
        {
            [SetUp]
            public void SetUp()
            {
                SetUpRequest(RouteNames.Users.ChangeApiKey, HttpMethod.Post, "api/session/changeApiKey");
                controller.User = new GenericPrincipal(new GenericIdentity("A"), new string[0]);
            }

            [Test]
            public void SetsKey()
            {
                store.Add(new ApiUser { Username = "A", Key = "old key" });

                var result = controller.ChangeApiKey(new KeyChangeRequest("new key")) as KeyChangeRequest;

                Assert.That(store.All.Single().Key, Is.EqualTo("new key"));
                Assert.That(result.Key, Is.EqualTo("new key"));
            }

            [Test]
            public void Generates()
            {
                store.Add(new ApiUser {Username = "A", Key = "old key"});

                var result = controller.ChangeApiKey(new KeyChangeRequest()) as KeyChangeRequest;

                Assert.That(result.Key, Is.Not.Empty);
                Assert.That(store.All.Single().Key, Is.EqualTo(result.Key));
            }

            [Test]
            public void NotFound()
            {
                var result = controller.ChangeApiKey(new KeyChangeRequest("new key")) as HttpResponseMessage;
                Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            }
        }

        public class PostTests : UsersControllerTests
        {
            [SetUp]
            public void SetUp()
            {
                SetUpRequest(RouteNames.Users.PostUser, HttpMethod.Post, "api/users/A");
            }

            [Test]
            public void NotFound()
            {
                var result = controller.Post("A", new UpdateUserAttributes { RenameTo = "B" });

                Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            }

            [Test]
            public void RenameUser()
            {
                const string key = "key";
                var roles = new[] { "role1" };

                store.Add(new ApiUser {Username = "A", Key = key, Roles = roles});

                controller.Post("A", new UpdateUserAttributes { RenameTo = "B" });

                Assert.That(store.All.Select(u => u.Username).ToArray(), Is.EquivalentTo(new[] {"B"}));
                Assert.That(store.All.Single().Key, Is.EqualTo(key));
                Assert.That(store.All.Single().Roles, Is.EquivalentTo(roles));
            }

            [Test]
            public void RenameUserOverwrite()
            {
                const string key = "key";
                var roles = new[] { "role1" };

                store.Add(new ApiUser { Username = "A", Key = key, Roles = roles });
                store.Add(new ApiUser { Username = "B", Key = key, Roles = roles });

                controller.Post("A", new UpdateUserAttributes { RenameTo = "B" });

                Assert.That(store.All.Select(u => u.Username).ToArray(), Is.EquivalentTo(new[] { "B" }));
                Assert.That(store.All.Single().Key, Is.EqualTo(key));
                Assert.That(store.All.Single().Roles, Is.EquivalentTo(roles));
            }

            [Test]
            public void RenameUserDoesNotOverwrite()
            {
                const string key = "key";
                var roles = new[] { "role1" };

                store.Add(new ApiUser { Username = "A", Key = key, Roles = roles });
                store.Add(new ApiUser { Username = "B", Key = key, Roles = roles });

                var result = controller.Post("A", new UpdateUserAttributes { RenameTo = "B", Overwrite = false});

                Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                Assert.That(store.All.Select(u => u.Username).ToArray(), Is.EquivalentTo(new[] { "A", "B" }));
            }

            [Test]
            public void RenameUserOverwritesOnCaseChange()
            {
                const string key = "key";
                var roles = new[] { "role1" };

                store.Add(new ApiUser { Username = "b", Key = key, Roles = roles });

                controller.Post("b", new UpdateUserAttributes { RenameTo = "B", Overwrite = false });

                Assert.That(store.All.Select(u => u.Username).ToArray(), Is.EquivalentTo(new[] { "B" }));
            }

            [Test]
            public void UpdateUserClearsRoles()
            {
                const string key = "key";
                var roles = new[] { "role1" };

                store.Add(new ApiUser { Username = "A", Key = key, Roles = roles });

                controller.Post("A", new UpdateUserAttributes { Overwrite = true });

                Assert.That(store.All.Select(u => u.Username).ToArray(), Is.EquivalentTo(new[] { "A" }));
                Assert.That(store.All.Single().Roles, Is.EquivalentTo(new String[0]));
            }
        }

        public class PutTests : UsersControllerTests
        {
            [SetUp]
            public void SetUp()
            {
                SetUpRequest(RouteNames.Users.PutUser, HttpMethod.Put, "api/users/A");
            }

            [Test]
            public void Overwrite()
            {
                const string key = "key";
                var roles = new[] { "role1" };

                store.Add(new ApiUser { Username = "A", Key = key, Roles = roles });

                var result = controller.Put("A", new UserAttributes { Key = "new key", Roles = new []{"role2"} });

                Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                Assert.That(store.All.Single().Key, Is.EqualTo("new key"));
                Assert.That(store.All.Single().Roles, Is.EquivalentTo(new[] {"role2"}));
            }

            [Test]
            public void DoesNotOverwrite()
            {
                const string key = "key";
                var roles = new[] { "role1" };

                store.Add(new ApiUser { Username = "A", Key = key, Roles = roles });

                var result = controller.Put("A", new UserAttributes { Overwrite = false });

                Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                Assert.That(store.All.Single().Key, Is.EqualTo(key));
                Assert.That(store.All.Single().Roles, Is.EquivalentTo(roles));
            }
        }
    }
}