using Microsoft.Extensions.DependencyInjection;

namespace PassManager.App.Services;

/// <summary>Creates view models with their dependencies resolved from the container.</summary>
public interface IViewModelFactory
{
    T Create<T>()
        where T : class;
}

public sealed class ViewModelFactory : IViewModelFactory
{
    private readonly IServiceProvider _services;

    public ViewModelFactory(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public T Create<T>()
        where T : class => _services.GetRequiredService<T>();
}
