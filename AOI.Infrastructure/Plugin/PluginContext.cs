using System.Reflection;
using System.Runtime.Loader;

namespace AOI.Infrastructure.Plugin;

public class PluginContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginContext(string pluginPath)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);

        if (path != null)
            return LoadFromAssemblyPath(path);

        return null;
    }
}