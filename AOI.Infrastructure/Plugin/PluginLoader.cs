using System.Reflection;

namespace AOI.Infrastructure.Plugin;

public static class PluginLoader
{
    public static IEnumerable<T> LoadPlugins<T>(string folder)
    {
        if (!Directory.Exists(folder))
            yield break;

        foreach (var dll in Directory.GetFiles(folder, "*.dll"))
        {
            var context = new PluginContext(dll);

            var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dll));

            foreach (var type in assembly.GetTypes())
            {
                if (typeof(T).IsAssignableFrom(type) &&
                    !type.IsInterface &&
                    !type.IsAbstract)
                {
                    yield return (T)Activator.CreateInstance(type)!;
                }
            }
        }
    }
}