using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Owin;
using NuGet.Lucene.Web.Authentication;

namespace NuGet.Lucene.Web.Middleware
{
    public class RoleMappingAuthenticationMiddleware : AuthenticationMiddlewareBase
    {
        public IUserStore Store { get; set; }
        public INuGetWebApiSettings Settings { get; set; }

        public RoleMappingAuthenticationMiddleware(OwinMiddleware next)
            : base(next)
        {
        }

        protected override AuthenticationHandlerBase CreateHandler()
        {
            return new RoleMappingAuthenticationHandler(Store, Settings.RoleMappings);
        }

        private class RoleMappingAuthenticationHandler : UserStoreAuthenticationHandler
        {
            private static readonly string[] Empty = new String[0];
            private readonly NameValueCollection roleMappings;

            public RoleMappingAuthenticationHandler(IUserStore store, NameValueCollection roleMappings)
                : base(store)
            {
                this.roleMappings = roleMappings;
            }

            protected override Task AuthenticateCoreAsync()
            {
                if (!IsAuthenticated)
                {
                    return completedTask;
                }

                var apiUser = store.FindByUsername(CurrentUsername);
                var isNew = false;

                if (apiUser == null)
                {
                    isNew = true;
                    apiUser = new ApiUser { Username = CurrentUsername };
                }

                var origRoles = (apiUser.Roles ?? Empty).ToArray();
                var missingRoles = RoleNames.All.Except(origRoles);
                var implicitGrants = GetMappedUserRoles(Request.User, missingRoles).ToArray();
                apiUser.Roles = origRoles.Union(implicitGrants).Distinct().ToArray();

                if (isNew || !apiUser.Roles.SequenceEqual(origRoles))
                {
                    store.Add(apiUser, UserUpdateMode.Overwrite);
                }

                if (apiUser.Roles.Any())
                {
                    Request.User = new SupplementalRolePrincipalWrapper(Request.User, apiUser.Roles);
                }

                return completedTask;
            }

            private IEnumerable<string> GetMappedUserRoles(IPrincipal user, IEnumerable<string> missingRoles)
            {
                return missingRoles.Where(role =>
                {
                    var aliases = (roleMappings.Get(role) ?? "")
                        .Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim());

                    var rolesToTest = new[] {role}.Union(aliases).Distinct();

                    return rolesToTest.Any(user.IsInRole);
                });
            }
        }
    }
}
