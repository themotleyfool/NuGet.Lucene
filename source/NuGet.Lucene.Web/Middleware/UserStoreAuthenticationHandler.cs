namespace NuGet.Lucene.Web.Middleware
{
    public abstract class UserStoreAuthenticationHandler : AuthenticationHandlerBase
    {
        protected readonly IUserStore store;

        protected UserStoreAuthenticationHandler(IUserStore store)
        {
            this.store = store;
        }
    }
}