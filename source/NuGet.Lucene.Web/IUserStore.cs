using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Lucene.Web.Authentication;

namespace NuGet.Lucene.Web
{
    public interface IUserStore : IDisposable
    {
        /// <summary>
        /// Gets or sets the string value of the local administrator username.
        /// </summary>
        string LocalAdministratorUsername { get; }

        /// <summary>
        /// Gets or sets the boolean value that indicates if requests on a 
        /// local address should automatically be granted administrative roles.
        /// </summary>
        bool HandleLocalRequestsAsAdmin { get; set; }

        /// <summary>
        /// Gets or sets the string value of the local administrators api key.
        /// </summary>
        string LocalAdministratorApiKey { get; set; }

        /// <summary>
        /// Gets the boolean value that indicates if the local administrator 
        /// user is enabled.
        /// </summary>
        bool IsLocalAdministratorEnabled { get; }

        /// <summary>
        /// Provides access to users to facilitate iteration over the collection.
        /// </summary>
        IEnumerable<ApiUser> All { get; }

        /// <summary>
        /// Configures the user store.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Gets a user by searching on username.
        /// </summary>
        ApiUser FindByUsername(string username);

        /// <summary>
        /// Gets a user by searching on api key.
        /// </summary>
        ApiUser FindByKey(string key);

        /// <summary>
        /// Adds a user to the store.
        /// </summary>
        void Add(ApiUser user);

        /// <summary>
        /// Adds a user to the store specifying the update mode.
        /// </summary>
        void Add(ApiUser user, UserUpdateMode mode);

        /// <summary>
        /// Updates a user by searching on username.
        /// </summary>
        void Update(string username, string newUsername, string key, string[] roles, UserUpdateMode mode);

        /// <summary>
        /// Deletes a user by searching on username.
        /// </summary>
        void Delete(string username);

        /// <summary>
        /// Deletes all users in the store.
        /// </summary>
        void DeleteAll();

        /// <summary>
        /// Changes the requested users api key.
        /// 
        /// The string returned is the value of the new api key.
        /// </summary>
        string ChangeApiKey(string username, string newKey);
    }
}
