using System;
using System.Linq;
using System.Net;
using NuGet.Lucene.Web.Models;
using NuGet.Lucene.Web.Util;

namespace NuGet.Lucene.Web
{
    public static class MirroringPackageRepositoryFactory
    {
        private const string UserAgent = "NuGet.Lucene.Web";

        public static IMirroringPackageRepository Create(IPackageRepository localRepository, string remotePackageUrl, TimeSpan timeout, bool alwaysCheckMirror)
        {
            if (string.IsNullOrWhiteSpace(remotePackageUrl))
            {
                return new NonMirroringPackageRepository(localRepository);
            }

            var remoteRepositories = remotePackageUrl.Split(';').Select(s => CreateDataServicePackageRepository(new HttpClient(new Uri(s)), timeout)).ToArray();

            if (alwaysCheckMirror)
            {
              return new EagerMirroringPackageRepository(localRepository, remoteRepositories, new WebCache());
            }

            return new MirroringPackageRepository(localRepository, remoteRepositories, new WebCache());
        }

        public static DataServicePackageRepository CreateDataServicePackageRepository(IHttpClient httpClient, TimeSpan timeout)
        {
            var userAgent = string.Format("{0}/{1} ({2})",
                                          UserAgent,
                                          typeof (MirroringPackageRepositoryFactory).Assembly.GetName().Version,
                                          Environment.OSVersion);

            var remoteRepository = new DataServicePackageRepository(httpClient);

            remoteRepository.SendingRequest += (s, e) =>
                {
                    e.Request.Timeout = (int) timeout.TotalMilliseconds;

                    ((HttpWebRequest) e.Request).UserAgent = userAgent;

                    e.Request.Headers.Add(RepositoryOperationNames.OperationHeaderName, RepositoryOperationNames.Mirror);
                };

            return remoteRepository;
        }
    }
}