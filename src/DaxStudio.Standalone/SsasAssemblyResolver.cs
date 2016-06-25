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
        private const string amoStrongNamePattern = "Microsoft.AnalysisServices, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
        private const string adomdStrongNamePattern = "Microsoft.AnalysisServices.AdomdClient, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
        private const int minVer = 11;
        private const int maxVer = 13;

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
            if (_assemblies.ContainsKey(name)) return _assemblies[name];
            return null;
        }

        private Assembly AddAssemblyToCache(string name)
        {
            if (_assemblies.ContainsKey(name)) return _assemblies[name];
            Assembly ass = null;
            if (name.StartsWith("Microsoft.AnalysisServices,"))
            {
                ass = GetAssembly(amoStrongNamePattern, 11, 13);
                _assemblies.Add(name, ass);
            }
            else if (name.StartsWith("Microsoft.AnalysisServices.AdomdClient,"))
            {
                ass = GetAssembly(adomdStrongNamePattern, 11, 13);
                _assemblies.Add(name, ass);
            }
            return null;
        }



        private Assembly GetAssembly(string format, int minVersion, int maxVersion)
        {
            for (int i = minVersion; i <= maxVersion; i++)
            {
                try
                {

                    Assembly assembly = Assembly.Load(string.Format(format, i));
                    if (assembly != null)
                    {
                        return assembly;
                    }
                }
                catch (Exception exception)
                {
                    Log.Warning( exception.Message + "\n");
                }
            }
            return null;
        }

        public void BuildAssemblyCache()
        {
            for (int i = minVer; i <= maxVer; i++)
            {
                AddAssemblyToCache(string.Format(adomdStrongNamePattern, i));
            }
            for (int i = minVer; i <= maxVer; i++)
            {
                AddAssemblyToCache(string.Format(amoStrongNamePattern, i));
            }
        }
    }
}