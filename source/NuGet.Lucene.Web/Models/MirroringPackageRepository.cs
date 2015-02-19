using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using NuGet.Lucene.Web.Util;

namespace NuGet.Lucene.Web.Models
{
    public interface IMirroringPackageRepository : IPackageLookup, IServiceBasedRepository
    {
        bool MirroringEnabled { get; }
    }

    public class NonMirroringPackageRepository : DelegatingPackageRepository, IMirroringPackageRepository
    {
        public NonMirroringPackageRepository(IPackageRepository target) : base(target)
        {
        }

        public bool MirroringEnabled { get { return false; } }
    }

    public class MirroringPackageRepository : DelegatingPackageRepository, IMirroringPackageRepository
    {
        private static readonly ILog Log = LogManager.GetLogger<MirroringPackageRepository>();

        private readonly IPackageRepository[] origins;

        private readonly ICache cache;

        public MirroringPackageRepository(IPackageRepository mirror, IPackageRepository[] origins, ICache cache)
            : base(mirror)
        {
            this.origins = origins;
            this.cache = cache;
        }

        public bool MirroringEnabled { get { return true; } }

        public override string Source
        {
            get { return string.Format("{0} auto-target of {1}", base.Source, string.Join(", ", origins.Select(o => o.Source))); }
        }

        public override bool SupportsPrereleasePackages
        {
            get { return base.SupportsPrereleasePackages || origins.Any(o => o.SupportsPrereleasePackages); }
        }

        public override IEnumerable<IPackage> FindPackagesById(string id)
        {
            var result = base.FindPackagesById(id).ToList();

            if (ShouldLookInOrigin(id, result))
            {
                var remotePackages = FindPackagesByIdInOrigin(id);
                return result
                    .Union(remotePackages, PackageEqualityComparer.IdAndVersion)
                    .Cast<IPackage>();
            }

            return result;
        }

        /// <summary>
        /// Determines if origin should be checked for additional package versions.
        /// The default implementation returns <c>true</c> if <paramref name="localPackages"/>
        /// is empty or if any of the local packages are mirrored.
        /// 
        /// An alternate implementation could use a whitelist or blacklist.
        /// </summary>
        /// <returns></returns>
        protected virtual bool ShouldLookInOrigin(string id, List<IPackage> localPackages)
        {
            return localPackages.IsEmpty() || localPackages.OfType<LucenePackage>().All(p => p.IsMirrored);
        }

        public override IPackage FindPackage(string packageId, SemanticVersion version)
        {
            var package = base.FindPackage(packageId, version);

            if (package != null) return package;

            package = FindPackageInOrigin(packageId, version);

            if (package == null) return null;

            Log.Info(m => m("Mirroring package {0} {1} from {2}", packageId, version, string.Join(", ", origins.Select(o => o.Source))));

            AddPackage(package);

            return base.FindPackage(package.Id, package.Version);
        }

        public virtual IEnumerable<IPackage> FindPackagesByIdInOrigin(string id)
        {
            var key = GetType().FullName + ":" + id.ToLowerInvariant();
            var result = cache.Get<IList<IPackage>>(key);

            if (result != null) return result;

            var foundPackages = new List<IPackage>();
            foreach (var origin in origins)
            {
                try
                {
                    result = origin.FindPackagesById(id).ToList();
                    cache.Add(key, result, TimeSpan.FromMinutes(5));
                    if (result.Any())
                    {
                        Log.Info(m => m("Found package {0} at {1}", id, origin.Source));
                        foundPackages.AddRange(result);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(m => m("Exception on FindPackagesById('{0}') for package origin {1}: {2}", id, origin.Source, ex.Message), ex);
                    return Enumerable.Empty<IPackage>();
                }
            }
            return foundPackages;
        }

        public virtual IPackage FindPackageInOrigin(string packageId, SemanticVersion version)
        {
            foreach (var origin in origins)
            {
                try
                {
                    var package =  origin.FindPackage(packageId, version);
                    if (package != null)
                    {
                      return package;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(m => m("Exception on FindPackage('{0}', '{1}') for package origin {2}: {3}", packageId, version, origin.Source, ex.Message), ex);
                    return null;
                }
            }
            return null;
        }
    }

    public class EagerMirroringPackageRepository : MirroringPackageRepository
    {
      public EagerMirroringPackageRepository(IPackageRepository mirror, IPackageRepository[] origins, ICache cache)
            : base(mirror, origins, cache)
        {
        }

        protected override bool ShouldLookInOrigin(string id, List<IPackage> localPackages)
        {
            return true;
        }
    }
}