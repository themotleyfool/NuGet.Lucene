using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Owin;
using NuGet.Lucene.Web.Util;

namespace NuGet.Lucene.Web.Middleware
{
    /// <summary>
    /// When request originates from a local network address,
    /// authenticate the user as <c>"LocalAdministrator"</c>
    /// and grant all roles in <see cref="RoleNames.All"/>.
    /// </summary>
    public class LocalRequestAuthenticationMiddleware : AuthenticationMiddlewareBase
    {
        public IUserStore Store { get; set; }

        public LocalRequestAuthenticationMiddleware(OwinMiddleware next)
            : base(next)
        {
        }

        protected override AuthenticationHandlerBase CreateHandler()
        {
            return new LocalRequestAuthenticationHandler(Store);
        }

        class LocalRequestAuthenticationHandler : UserStoreAuthenticationHandler
        {
            public LocalRequestAuthenticationHandler(IUserStore store)
                : base(store)
            {
            }

            protected override Task AuthenticateCoreAsync()
            {
                if (IsAuthenticated || !Request.IsLocal())
                {
                    return completedTask;
                }

                var identity = new GenericIdentity(store.LocalAdministratorUsername, typeof(LocalRequestAuthenticationMiddleware).Name);
                Request.User = new GenericPrincipal(identity, RoleNames.All.ToArray());

                return completedTask;
            }

        }
    }
}