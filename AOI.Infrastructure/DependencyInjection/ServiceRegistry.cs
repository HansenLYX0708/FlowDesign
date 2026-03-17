using Microsoft.Extensions.DependencyInjection;
using System;

namespace AOI.Infrastructure.DependencyInjection;

public static class ServiceRegistry
{
    private static ServiceProvider? _provider;

    public static IServiceCollection Services { get; } = new ServiceCollection();

    public static void Build()
    {
        _provider = Services.BuildServiceProvider();
    }

    public static T Get<T>()
    {
        return _provider!.GetRequiredService<T>();
    }
}