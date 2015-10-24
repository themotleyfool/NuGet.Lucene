using System.Linq;
using Lucene.Net.Linq;
using Lucene.Net.Store;
using Lucene.Net.Util;
using NuGet.Lucene.Web.Authentication;
using NUnit.Framework;

namespace NuGet.Lucene.Web.Tests
{
    [TestFixture]
    public class UserStoreTests
    {
        private LuceneDataProvider provider;
        private IUserStore store;

        [SetUp]
        public void SetUp()
        {
            provider = new LuceneDataProvider(new RAMDirectory(), Version.LUCENE_30);
            store = new UserStore(provider);
        }

        public class InitTests : UserStoreTests
        {
            [Test]
            public void CreatesLocalAdministratorWithFixedKey()
            {
                store.LocalAdministratorApiKey = "hard-coded";

                store.Initialize();

                Assert.That(store.FindByKey("hard-coded"), Is.Not.Null);
            }

            [Test]
            public void CreatesLocalAdministratorWithKey()
            {
                store.HandleLocalRequestsAsAdmin = true;

                store.Initialize();

                var user = store.FindByUsername(store.LocalAdministratorUsername);
                Assert.That(user, Is.Not.Null);
                Assert.That(user.Key, Is.Not.Empty);
            }

            [Test]
            public void OverwritesApiKeyWhenFixed()
            {
                store.LocalAdministratorApiKey = "fixed";
                AddUser(store.LocalAdministratorUsername, "wrong");

                store.Initialize();

                var user = store.FindByKey("fixed");
                Assert.That(user, Is.Not.Null);
            }

            [Test]
            public void DoesNotChangeExistingKey()
            {
                store.HandleLocalRequestsAsAdmin = true;
                AddUser(store.LocalAdministratorUsername, "existing");

                store.Initialize();

                Assert.That(store.FindByUsername(store.LocalAdministratorUsername).Key, Is.EqualTo("existing"));
            }

            [Test]
            public void DoesNotCreateLocalAdministratorWhenNotEnabled()
            {
                store.HandleLocalRequestsAsAdmin = false;
                store.LocalAdministratorApiKey = null;

                store.Initialize();

                Assert.That(store.All, Is.Empty);
            }

            [Test]
            public void GrantsAllRoles()
            {
                store.HandleLocalRequestsAsAdmin = true;

                store.Initialize();

                Assert.That(store.FindByUsername(store.LocalAdministratorUsername).Roles, Is.EquivalentTo(RoleNames.All));
            }

            [Test]
            public void ReplacesAllRoles()
            {
                store.HandleLocalRequestsAsAdmin = true;
                AddUser(store.LocalAdministratorUsername, "existing", new[] {"old-role"});

                store.Initialize();

                Assert.That(store.FindByUsername(store.LocalAdministratorUsername).Roles, Is.EquivalentTo(RoleNames.All));
            }
        }

        public class UpdateTests : UserStoreTests
        {
            [Test]
            public void CannotOverwriteLocalAdministrator()
            {
                AddUser(store.LocalAdministratorUsername);
                AddUser("dummy");

                TestDelegate call = () => store.Update("dummy", store.LocalAdministratorUsername, null, null, UserUpdateMode.Overwrite);

                Assert.That(call, Throws.InstanceOf<UserPermissionException>());
            }

            [Test]
            public void CannotChangeLocalAdministratorRoles()
            {
                AddUser(store.LocalAdministratorUsername);

                TestDelegate call = () => store.Update(store.LocalAdministratorUsername, null, null, new[] {"new-role"}, UserUpdateMode.NoClobber);

                Assert.That(call, Throws.InstanceOf<UserPermissionException>());
            }

            [Test]
            public void CannotRenameLocalAdministrator()
            {
                AddUser(store.LocalAdministratorUsername);

                TestDelegate call = () => store.Update(store.LocalAdministratorUsername, "dummy", null, null, UserUpdateMode.NoClobber);

                Assert.That(call, Throws.InstanceOf<UserPermissionException>());
            }

            [Test]
            public void CanChangeLocalAdministratorKeyWhenNotFixed()
            {
                store.LocalAdministratorApiKey = null;
                AddUser(store.LocalAdministratorUsername);

                store.Update(store.LocalAdministratorUsername, null, "new-key", null, UserUpdateMode.NoClobber);

                Assert.That(store.All.Single().Key, Is.EqualTo("new-key"));
            }

            [Test]
            public void IgnoresNoOpChangesToLocalAdministrator()
            {
                store.LocalAdministratorApiKey = "fixed";
                store.HandleLocalRequestsAsAdmin = true;
                store.Initialize();
                var user = store.All.Single();

                TestDelegate call = () => store.Update(user.Username, null, user.Key, user.Roles.ToArray(), UserUpdateMode.NoClobber);

                Assert.That(call, Throws.Nothing);
            }

            [Test]
            public void CannotChangeLocalAdministratorKeyWhenNotFixed()
            {
                store.LocalAdministratorApiKey = "fixed-key";
                AddUser(store.LocalAdministratorUsername, store.LocalAdministratorApiKey);

                TestDelegate call = () => store.Update(store.LocalAdministratorUsername, null, "new-key", null, UserUpdateMode.NoClobber);

                Assert.That(call, Throws.InstanceOf<UserPermissionException>());
                Assert.That(store.All.Single().Key, Is.EqualTo("fixed-key"));
            }

            [Test]
            public void NoClobberMakesNoModifications()
            {
                AddUser("dummy1", "oldkey1", new [] {"old-role1"});
                AddUser("dummy2", "oldkey2", new [] {"old-role2"});

                TestDelegate call = () => store.Update("dummy2", "dummy1", "newkey", new[] {"new-role"}, UserUpdateMode.NoClobber);

                Assert.That(call, Throws.InstanceOf<UserOverwriteException>());
                Assert.That(store.FindByUsername("dummy2").Key, Is.EqualTo("oldkey2"));
                Assert.That(store.FindByUsername("dummy2").Roles, Is.EqualTo(new[] {"old-role2"}));
            }
        }

        public class DeleteTests : UserStoreTests
        {
            [Test]
            public void CannotDeleteLocalAdministrator()
            {
                AddUser(store.LocalAdministratorUsername);

                TestDelegate call = () => store.Delete(store.LocalAdministratorUsername);

                Assert.That(call, Throws.InstanceOf<UserPermissionException>());
            }

            [Test]
            public void DeleteAllLeavesLocalAdministrator()
            {
                AddUser("dummy");
                AddUser(store.LocalAdministratorUsername);

                store.DeleteAll();

                Assert.That(store.All.Select(u => u.Username), Is.EquivalentTo(new[] {store.LocalAdministratorUsername}));
            }

        }

        public class AddTests : UserStoreTests
        {
            [Test]
            public void CannotOverwriteLocalAdministrator()
            {
                AddUser(store.LocalAdministratorUsername);

                TestDelegate call = () => store.Add(store.All.Single(), UserUpdateMode.Overwrite);

                Assert.That(call, Throws.InstanceOf<UserPermissionException>());
            }
        }

        public class ChangeApiKeyTests : UserStoreTests
        {
            [Test]
            public void CannotChangeFixedApiKey()
            {
                store.LocalAdministratorApiKey = "fixed";
                AddUser(store.LocalAdministratorUsername, store.LocalAdministratorApiKey);

                TestDelegate call = () => store.ChangeApiKey(store.LocalAdministratorUsername, "new");

                Assert.That(call, Throws.InstanceOf<UserPermissionException>());
                Assert.That(store.All.Single().Key, Is.EqualTo(store.LocalAdministratorApiKey));
            }

            [Test]
            public void CanChangeNonFixedApiKey()
            {
                store.LocalAdministratorApiKey = null;
                AddUser(store.LocalAdministratorUsername, store.LocalAdministratorUsername);

                TestDelegate call = () => store.ChangeApiKey(store.LocalAdministratorUsername, "new");

                Assert.That(call, Throws.Nothing);
                Assert.That(store.All.Single().Key, Is.EqualTo("new"));

            }
        }

        protected void AddUser(string username, string key = "", string[] roles=null)
        {
            using (var session = provider.OpenSession<ApiUser>())
            {
                session.Add(new ApiUser {Username = username, Key = key, Roles = roles});
            }
        }
    }
}