using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Lucene.Net.Linq;
using NuGet.Lucene.Web.Authentication;

namespace NuGet.Lucene.Web
{
    public class UserOverwriteException : Exception {}
    public class UserNotFoundException : Exception {}

    public class UserPermissionException : Exception
    {
        public UserPermissionException(string message) : base(message)
        {
        }
    }

    public enum UserUpdateMode { Overwrite, NoClobber }

    public class UserStore : IUserStore
    {
        private readonly LuceneDataProvider provider;

        public string LocalAdministratorUsername
        {
            get { return "LocalAdministrator"; }
        }

        public bool HandleLocalRequestsAsAdmin { get; set; }

        public string LocalAdministratorApiKey { get; set; }

        public bool IsLocalAdministratorEnabled
        {
            get { return HandleLocalRequestsAsAdmin || !string.IsNullOrWhiteSpace(LocalAdministratorApiKey); }
        }

        public IEnumerable<ApiUser> All
        {
            get { return Users; }
        }

        public UserStore(LuceneDataProvider provider)
        {
            this.provider = provider;
        }
        
        public void Initialize()
        {
            if (!IsLocalAdministratorEnabled) return;

            var user = FindByUsername(LocalAdministratorUsername)
                ?? new ApiUser { Username = LocalAdministratorUsername};

            if (!string.IsNullOrWhiteSpace(LocalAdministratorApiKey))
            {
                user.Key = LocalAdministratorApiKey;
            }

            user.Roles = RoleNames.All;
            AddInternal(user, UserUpdateMode.Overwrite, disableProtectedAccountChecks:true);
        }

        public ApiUser FindByUsername(string username)
        {
            return Users.SingleOrDefault(u => u.Username == ScrubUsername(username));
        }

        public ApiUser FindByKey(string key)
        {
            return Users.SingleOrDefault(u => u.Key == key);
        }

        public void Add(ApiUser user)
        {
            Add(user, UserUpdateMode.Overwrite);
        }

        public void Add(ApiUser user, UserUpdateMode mode)
        {
            AddInternal(user, mode);
        }

        private void AddInternal(ApiUser user, UserUpdateMode mode, bool disableProtectedAccountChecks=false)
        {
            user.Username = ScrubUsername(user.Username);

            if (string.IsNullOrWhiteSpace(user.Key))
            {
                user.Key = Guid.NewGuid().ToString();
            }

            using (var session = OpenSession())
            {
                var userExists = session.Query().Any(u => u.Username == user.Username);

                if (mode == UserUpdateMode.NoClobber && userExists)
                {
                    throw new UserOverwriteException();
                }

                if (userExists && !disableProtectedAccountChecks && IsProtectedAccount(user.Username))
                {
                    throw new UserPermissionException(user.Username + " account cannot be overwritten.");
                }

                session.Add(user);
            }
        }

        public void Update(string username, string newUsername, string key, string[] roles, UserUpdateMode mode)
        {
            username = ScrubUsername(username);
            newUsername = ScrubUsername(newUsername);

            using (var session = OpenSession())
            {
                var user = session.Query().SingleOrDefault(u => u.Username == username);

                if (user == null)
                {
                    throw new UserNotFoundException();
                }

                var isRenamingToDifferentName = !string.IsNullOrWhiteSpace(newUsername) && !newUsername.Equals(username, StringComparison.InvariantCultureIgnoreCase);

                if (isRenamingToDifferentName && mode == UserUpdateMode.NoClobber && session.Query().Any(u => u.Username == newUsername))
                {
                    throw new UserOverwriteException();
                }

                if (isRenamingToDifferentName && IsProtectedAccount(newUsername))
                {
                    throw new UserPermissionException(newUsername + " cannot be overwritten.");
                }

                if (isRenamingToDifferentName && IsProtectedAccount(user.Username))
                {
                    throw new UserPermissionException(user.Username + " cannot be renamed.");
                }

                if (roles != null && !roles.SequenceEqual(user.Roles))
                {
                    if (IsProtectedAccount(user.Username))
                    {
                        throw new UserPermissionException("Cannot modify roles of protected account " + user.Username + ".");
                    }
                    user.Roles = roles;
                }

                if (key != null && !key.Equals(user.Key))
                {
                    if (IsApiKeyUnmodifiable(user.Username))
                    {
                        throw new UserPermissionException("API Key for account " + user.Username + " cannot be modified.");
                    }
                    user.Key = key;
                }

                if (!string.IsNullOrWhiteSpace(newUsername))
                {
                    user.Username = newUsername;
                }
            }
        }

        public void Delete(string username)
        {
            username = ScrubUsername(username);

            using (var session = OpenSession())
            {
                var user = session.Query().SingleOrDefault(u => u.Username == username);

                if (user == null)
                {
                    throw new UserNotFoundException();
                }

                if (IsProtectedAccount(user.Username))
                {
                    throw new UserPermissionException(user.Username + " account cannot be deleted.");
                }

                session.Delete(user);
            }
        }

        public void DeleteAll()
        {
            using (var session = OpenSession())
            {
                var nonProtectedAccounts = session.Query().ToList().Where(u => !IsProtectedAccount(u.Username));
                session.Delete(nonProtectedAccounts.ToArray());
            }
        }

        public string ChangeApiKey(string username, string newKey)
        {
            username = ScrubUsername(username);
            if (IsApiKeyUnmodifiable(username))
            {
                throw new UserPermissionException("API Key for account " + username + " cannot be modified.");
            }

            if (string.IsNullOrWhiteSpace(newKey))
            {
                newKey = Guid.NewGuid().ToString();
            }

            using (var session = OpenSession())
            {
                var apiUser = session.Query().SingleOrDefault(u => u.Username == username);

                if (apiUser == null)
                {
                    throw new UserNotFoundException();
                }

                apiUser.Key = newKey;
            }

            return newKey;
        }

        protected IQueryable<ApiUser> Users
        {
            get
            {
                return provider.AsQueryable<ApiUser>();
            }
        }

        protected ISession<ApiUser> OpenSession()
        {
            return provider.OpenSession<ApiUser>();
        }

        protected virtual string ScrubUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return "";
            return username.Replace('\\', '/');
        }

        protected virtual bool IsProtectedAccount(string username)
        {
            return LocalAdministratorUsername.Equals(username, StringComparison.InvariantCultureIgnoreCase);
        }

        protected virtual bool IsApiKeyUnmodifiable(string username)
        {
            return IsProtectedAccount(username) && !string.IsNullOrWhiteSpace(LocalAdministratorApiKey);
        }

        public void Dispose()
        {
            LogManager.GetCurrentClassLogger().Info("Stopping UserStore indexing services.");

            provider.Dispose();
        }
    }
}