using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Lucene.Web.Symbols
{
    public class SymbolSourceMapper
    {

        /// <summary>
        /// Finds a source file in the symbol package that matches the referenced source file from a PDB.
        /// </summary>
        /// <param name="referencedSource">
        /// A full path to a source file referenced from a PDB.
        /// </param>
        /// <param name="sourceFiles">
        /// A list of source files that were included in the symbol package,
        /// relative to the <c>"src"</c> directory in the package.
        /// </param>
        /// <returns>
        /// Path to the file, relative to the <c>"src"</c> directory, or <c>string.Empty</c>
        /// if no match is found.
        /// </returns>
        public string FindSourceFile(string referencedSource, ISet<string> sourceFiles)
        {
            var parts = referencedSource.Split(new[] {'/', '\\'});
            var c = 0;
            var i = parts[0].Length + 1;

            while (++c < parts.Length)
            {
                var relPath = referencedSource.Substring(i);
                if (sourceFiles.Contains(relPath, StringComparer.InvariantCultureIgnoreCase))
                {
                    return relPath;
                }
                i += parts[c].Length + 1;
            }

            return string.Empty;
        }

        public string CreateSourceMappingIndex(IPackageName package, string symbolSourceUri, List<string> referencedSources, ISet<string> sourceFiles)
        {
            var version = package.Version;
            var packageId = package.Id;
            
            var sb = new StringBuilder();

            sb.AppendLine("SRCSRV: ini ------------------------------------------------");
            sb.AppendLine("VERSION=2");
            sb.AppendLine("INDEXVERSION=2");
            sb.AppendLine("VERCTRL=http");
            sb.AppendFormat("DATETIME={0}" + Environment.NewLine, DateTime.UtcNow);
            sb.AppendLine("SRCSRV: variables ------------------------------------------");
            sb.AppendLine("SRCSRVVERCTRL=http");
            sb.AppendFormat("SRCSRVTRG={0}/%var4%/%var2%/%var5%" + Environment.NewLine,symbolSourceUri);
            sb.AppendLine("SRCSRVCMD=");
            sb.AppendLine("SRCSRV: source files ---------------------------------------");

            foreach (var source in referencedSources)
            {
                var relativePath = FindSourceFile(source, sourceFiles);
                if (string.IsNullOrWhiteSpace(relativePath)) continue;
                sb.AppendFormat("{0}*{1}*_*{2}*{3}" + Environment.NewLine, source, version, packageId, relativePath);
            }

            sb.AppendLine("SRCSRV: end ------------------------------------------------");

            return sb.ToString();
        }
    }
}
