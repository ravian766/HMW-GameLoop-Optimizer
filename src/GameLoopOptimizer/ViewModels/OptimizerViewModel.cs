using System.Collections.ObjectModel;
using System.Windows.Input;
using GameLoopOptimizer.Core;
using GameLoopOptimizer.Models;
using GameLoopOptimizer.Optimizations;

namespace GameLoopOptimizer.ViewModels;

public class OptimizerViewModel : ViewModelBase
{
    private readonly Func<HardwareInfo> _getHw;
    private readonly Func<SystemInfo> _getSys;
    private readonly Func<GameLoopConfig> _getGl;
    private readonly List<OptimizationCardViewModel> _allCards = new();

    public ObservableCollection<OptimizationCardViewModel> VisibleCards { get; } = new();

    private OptimizationProfile _currentProfile = OptimizationProfile.Balanced;
    public OptimizationProfile CurrentProfile
    {
        get => _currentProfile;
        set
        {
            if (SetProperty(ref _currentProfile, value))
            {
                ApplyProfileSelection(value);
            }
        }
    }

    private string _selectedCategory = "All";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                FilterCards();
            }
        }
    }

    private bool _isOptimizing;
    public bool IsOptimizing
    {
        get => _isOptimizing;
        set => SetProperty(ref _isOptimizing, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand OptimizeSelectedCommand { get; }
    public ICommand RestoreAllCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand SelectProfileCommand { get; }

    public event EventHandler? OptimizationsChanged;

    public OptimizerViewModel(
        List<IOptimizationModule> modules, 
        Func<HardwareInfo> getHw, 
        Func<SystemInfo> getSys, 
        Func<GameLoopConfig> getGl)
    {
        _getHw = getHw;
        _getSys = getSys;
        _getGl = getGl;

        foreach (var mod in modules)
        {
            var card = new OptimizationCardViewModel(mod, getHw, getSys, getGl);
            card.OptimizationCompleted += (s, e) => OptimizationsChanged?.Invoke(this, EventArgs.Empty);
            _allCards.Add(card);
        }

        OptimizeSelectedCommand = new AsyncRelayCommand(OptimizeSelectedAsync);
        RestoreAllCommand = new AsyncRelayCommand(RestoreAllAsync);

        SelectAllCommand = new RelayCommand(() =>
        {
            foreach (var c in _allCards) c.IsSelected = true;
        });

        DeselectAllCommand = new RelayCommand(() =>
        {
            foreach (var c in _allCards) c.IsSelected = false;
        });

        SelectProfileCommand = new RelayCommand(p =>
        {
            if (p is OptimizationProfile prof)
            {
                CurrentProfile = prof;
            }
        });

        ApplyProfileSelection(CurrentProfile);
        FilterCards();
    }

    public async Task AnalyzeAllAsync()
    {
        var hw = _getHw();
        var sys = _getSys();
        var gl = _getGl();

        foreach (var card in _allCards)
        {
            await card.Module.AnalyzeAsync(hw, sys, gl);
            card.RefreshProperties();
        }
    }

    private void ApplyProfileSelection(OptimizationProfile profile)
    {
        foreach (var card in _allCards)
        {
            switch (profile)
            {
                case OptimizationProfile.Safe:
                    card.IsSelected = card.RiskLevel == RiskLevel.Safe;
                    break;
                case OptimizationProfile.Balanced:
                    card.IsSelected = card.RiskLevel == RiskLevel.Safe || card.RiskLevel == RiskLevel.Low;
                    break;
                case OptimizationProfile.MaximumPerformance:
                    card.IsSelected = true;
                    break;
                case OptimizationProfile.Custom:
                    // Keep user selection
                    break;
            }
        }
    }

    private void FilterCards()
    {
        VisibleCards.Clear();
        foreach (var card in _allCards)
        {
            if (SelectedCategory == "All" ||
                (SelectedCategory == "Windows" && card.Category == OptimizationCategory.WindowsConfig) ||
                (SelectedCategory == "Power" && card.Category == OptimizationCategory.PowerDelivery) ||
                (SelectedCategory == "GameLoop" && card.Category == OptimizationCategory.GameLoopEngine) ||
                (SelectedCategory == "Graphics" && card.Category == OptimizationCategory.GraphicsQuality) ||
                (SelectedCategory == "Memory" && card.Category == OptimizationCategory.MemoryStorage) ||
                (SelectedCategory == "Background" && card.Category == OptimizationCategory.BackgroundProcess))
            {
                VisibleCards.Add(card);
            }
        }
    }

    public async Task OptimizeSelectedAsync()
    {
        IsOptimizing = true;
        StatusMessage = "Applying optimizations...";
        int successCount = 0;

        var hw = _getHw();
        var sys = _getSys();
        var gl = _getGl();

        try
        {
            foreach (var card in _allCards.Where(c => c.IsSelected))
            {
                card.IsBusy = true;
                try
                {
                    var res = await card.Module.ApplyAsync(hw, sys, gl);
                    if (res.Success) successCount++;
                    card.RefreshProperties();
                }
                finally
                {
                    card.IsBusy = false;
                }
            }

            StatusMessage = $"Successfully optimized {successCount} modules.";
            OptimizationsChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsOptimizing = false;
        }
    }

    public async Task RestoreAllAsync()
    {
        IsOptimizing = true;
        StatusMessage = "Restoring settings...";

        try
        {
            int restored = await RestoreManager.RestoreAllAsync();
            await AnalyzeAllAsync();
            StatusMessage = $"Restored {restored} settings to original configuration.";
            OptimizationsChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsOptimizing = false;
        }
    }
}
