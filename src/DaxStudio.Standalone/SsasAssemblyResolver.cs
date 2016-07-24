using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DaxStudio.Standalone
{
    internal sealed class SsasAssemblyResolver
    {
        private static readonly SsasAssemblyResolver instance = new SsasAssemblyResolver();
        private readonly Dictionary<string,Assembly> _assemblies = new Dictionary<string,Assembly>();
        //private const string amoStrongNamePattern = "Microsoft.AnalysisServices, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
        //private const string adomdStrongNamePattern = "Microsoft.AnalysisServices.AdomdClient, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
        //private const int minVer = 11;
        //private const int maxVer = 13;
        private List<string> _resolving = new List<string>();
        private object _mutex = new object();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static SsasAssemblyResolver()
        {
        }

        private SsasAssemblyResolver()
        {
        }

        public static SsasAssemblyResolver Instance
        {
            get
            {
                return instance;
            }
        }

        public Assembly Resolve(string name)
        {
            bool addToCache = false;
            if (_assemblies.ContainsKey(name)) return _assemblies[name];
            // double lock before adding an assembly to the cache to make 100% sure we are threadsafe
            if (!_resolving.Contains(name))
            {
                lock(_mutex)
                {
                    if (!_resolving.Contains(name))
                    {
                        _resolving.Add(name);
                        addToCache = true;
                    }
                }
            }

            if (addToCache) return AddAssemblyToCache(name);
            return null;
        }

        private Assembly AddAssemblyToCache(string name)
        {
            if (_assemblies.ContainsKey(name)) return _assemblies[name];
            Assembly ass = null;
            if (name.StartsWith("Microsoft.AnalysisServices,") || name.StartsWith("Microsoft.AnalysisServices.AdomdClient,"))
            {
                var an = new AssemblyName(name);
                ass = GetAssembly(an);
                if (ass != null) _assemblies.Add(name, ass);
                return ass;
            }
            return null;
        }

/// <summary>
/// Searches the for the next two major versions of an assembly
/// </summary>
/// <param name="assemblyName"></param>
/// <returns>Assembly</returns>
        private Assembly GetAssembly(AssemblyName assemblyName)
        {
            int minVer = assemblyName.Version.Major;
            for (int i = 1; i <= 2; i++)
            {
                try
                {
                    assemblyName.Version = new Version(minVer + 1, 0, 0, 0);
                    Assembly assembly = Assembly.Load(assemblyName);
                    if (assembly != null) return assembly;

                }
                catch (Exception ex)
                {
                    Log.Warning(ex.Message);
                }
            }
            return null;
        }

    }
}