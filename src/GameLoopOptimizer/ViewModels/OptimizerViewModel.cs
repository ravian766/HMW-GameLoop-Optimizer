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
    private readonly Func<PerformanceMetrics>? _getMetrics;
    private readonly List<OptimizationCardViewModel> _allCards = new();

    private OptimizationReport? _latestReport;
    public OptimizationReport? LatestReport
    {
        get => _latestReport;
        set => SetProperty(ref _latestReport, value);
    }

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
        Func<GameLoopConfig> getGl,
        Func<PerformanceMetrics>? getMetrics = null)
    {
        _getHw = getHw;
        _getSys = getSys;
        _getGl = getGl;
        _getMetrics = getMetrics;

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
                case OptimizationProfile.Competitive:
                    // Prioritize low-latency, timer, CPU affinity, GPU scheduling, and power delivery
                    card.IsSelected = card.Category == OptimizationCategory.WindowsConfig ||
                                      card.Category == OptimizationCategory.PowerDelivery ||
                                      card.Category == OptimizationCategory.GameLoopEngine ||
                                      card.RiskLevel != RiskLevel.Advanced;
                    break;
                case OptimizationProfile.LowEndPC:
                    // Safe optimizations only, focusing on RAM freeing, storage cleanup, and essential debloat
                    card.IsSelected = card.RiskLevel == RiskLevel.Safe &&
                                      (card.Category == OptimizationCategory.MemoryStorage ||
                                       card.Category == OptimizationCategory.WindowsConfig ||
                                       card.Category == OptimizationCategory.BackgroundProcess);
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

    public async Task<OptimizationReport> OptimizeSelectedAsync()
    {
        IsOptimizing = true;
        StatusMessage = "Applying system & GameLoop optimizations...";
        int successCount = 0;
        int failCount = 0;

        var hw = _getHw();
        var sys = _getSys();
        var gl = _getGl();

        var selectedCards = _allCards.Where(c => c.IsSelected).ToList();
        var rec = RecommendationEngine.Calculate(hw);

        var report = new OptimizationReport
        {
            Timestamp = DateTime.Now,
            ProfileName = CurrentProfile.ToString(),
            TotalConsidered = selectedCards.Count,
            ScoreBefore = ScoringEngine.CalculateScore(hw, sys, gl, rec).TotalScore
        };

        var metricsBefore = _getMetrics?.Invoke() ?? new PerformanceMetrics();
        var snapBefore = OptimizationSnapshot.Capture(
            metricsBefore, sys, _allCards.Count(c => c.IsOptimized), _allCards.Count, "Pre-Optimization");

        try
        {
            foreach (var card in selectedCards)
            {
                card.IsBusy = true;
                string prev = card.CurrentStateDisplay;
                try
                {
                    var res = await card.Module.ApplyAsync(hw, sys, gl);
                    if (res.Success) successCount++;
                    else failCount++;

                    card.RefreshProperties();

                    report.Items.Add(new OptimizationReportItem
                    {
                        ModuleId = card.Module.Id,
                        Title = card.Title,
                        Category = card.Category,
                        Success = res.Success,
                        Message = res.Message,
                        PreviousState = prev,
                        NewState = card.CurrentStateDisplay
                    });
                }
                catch (Exception ex)
                {
                    failCount++;
                    report.Items.Add(new OptimizationReportItem
                    {
                        ModuleId = card.Module.Id,
                        Title = card.Title,
                        Category = card.Category,
                        Success = false,
                        Message = ex.Message,
                        PreviousState = prev,
                        NewState = card.CurrentStateDisplay
                    });
                }
                finally
                {
                    card.IsBusy = false;
                }
            }

            report.AppliedCount = successCount;
            report.FailedCount = failCount;

            var metricsAfter = _getMetrics?.Invoke() ?? new PerformanceMetrics();
            var snapAfter = OptimizationSnapshot.Capture(
                metricsAfter, sys, _allCards.Count(c => c.IsOptimized), _allCards.Count, "Post-Optimization");

            report.SnapshotComparison = new SnapshotComparison(snapBefore, snapAfter);
            report.ScoreAfter = ScoringEngine.CalculateScore(hw, sys, gl, rec).TotalScore;

            LatestReport = report;
            StatusMessage = report.SummaryText;
            OptimizationsChanged?.Invoke(this, EventArgs.Empty);

            Logger.Success("OptimizationReport", report.SummaryText);
            return report;
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
