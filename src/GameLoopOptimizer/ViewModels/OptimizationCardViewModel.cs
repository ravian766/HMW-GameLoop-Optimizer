using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;

namespace GameLoopOptimizer.ViewModels;

public class OptimizationCardViewModel : ViewModelBase
{
    public IOptimizationModule Module { get; }

    public string Id => Module.Id;
    public string Title => Module.Title;
    public OptimizationCategory Category => Module.Category;
    public RiskLevel RiskLevel => Module.RiskLevel;
    public string Description => Module.Description;
    public string TechnicalRationale => Module.TechnicalRationale;
    public bool RequiresAdmin => Module.RequiresAdmin;

    public string CurrentStateDisplay => Module.CurrentStateDisplay;
    public string RecommendedStateDisplay => Module.RecommendedStateDisplay;
    public bool IsOptimized => Module.IsOptimized;
    public OptimizationState State => Module.State;

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string RiskColor => RiskLevel switch
    {
        RiskLevel.Safe => "#00E676",       // Vibrant Emerald
        RiskLevel.Low => "#00E5FF",        // Cyan
        RiskLevel.Moderate => "#FFB300",   // Amber
        RiskLevel.Advanced => "#FF5252",   // Coral
        _ => "#888888"
    };

    public string StatusBadgeColor => State switch
    {
        OptimizationState.Optimized => "#00E676",
        OptimizationState.Recommended => "#00E5FF",
        OptimizationState.NotDetected => "#888888",
        OptimizationState.RequiresAdmin => "#FF9100",
        OptimizationState.NotOptimized => "#FF5252",
        _ => "#B0BEC5"
    };

    public ICommand ApplyCommand { get; }
    public ICommand RollbackCommand { get; }

    public event EventHandler? OptimizationCompleted;

    public OptimizationCardViewModel(IOptimizationModule module, Func<HardwareInfo> getHw, Func<SystemInfo> getSys, Func<GameLoopConfig> getGl)
    {
        Module = module;

        ApplyCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            try
            {
                var res = await Module.ApplyAsync(getHw(), getSys(), getGl());
                RefreshProperties();
                OptimizationCompleted?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                IsBusy = false;
            }
        });

        RollbackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            try
            {
                var res = await Module.RollbackAsync(null);
                RefreshProperties();
                OptimizationCompleted?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                IsBusy = false;
            }
        });
    }

    public void RefreshProperties()
    {
        OnPropertyChanged(nameof(CurrentStateDisplay));
        OnPropertyChanged(nameof(RecommendedStateDisplay));
        OnPropertyChanged(nameof(IsOptimized));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StatusBadgeColor));
    }
}
