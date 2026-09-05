using System.Reflection;
using GameLoopOptimizer.Optimizations;

namespace GameLoopOptimizer.Core;

/// <summary>
/// Centralized registry and factory for all IOptimizationModule implementations.
/// Provides dynamic discovery via reflection to eliminate hardcoded module lists.
/// </summary>
public static class OptimizationModuleRegistry
{
    private static readonly List<Type> _cachedModuleTypes;

    static OptimizationModuleRegistry()
    {
        _cachedModuleTypes = typeof(IOptimizationModule).Assembly
            .GetTypes()
            .Where(t => typeof(IOptimizationModule).IsAssignableFrom(t) 
                     && !t.IsInterface 
                     && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();
    }

    /// <summary>
    /// Gets all concrete module types discovered in the assembly.
    /// </summary>
    public static IReadOnlyList<Type> GetModuleTypes() => _cachedModuleTypes;

    /// <summary>
    /// Creates instances of all registered optimization modules.
    /// </summary>
    public static List<IOptimizationModule> CreateAllModules()
    {
        var modules = new List<IOptimizationModule>();

        foreach (var type in _cachedModuleTypes)
        {
            try
            {
                if (Activator.CreateInstance(type) is IOptimizationModule module)
                {
                    modules.Add(module);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ModuleRegistry", $"Failed to instantiate module {type.FullName}: {ex.Message}");
            }
        }

        return modules;
    }
}
