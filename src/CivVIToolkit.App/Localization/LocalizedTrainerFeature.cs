using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CivVIToolkit.Core.Trainer;
using Microsoft.UI.Xaml;

namespace CivVIToolkit.App.Localization;

public sealed class LocalizedTrainerFeature : INotifyPropertyChanged
{
    private string _group = string.Empty;
    private string _scope = string.Empty;
    private string _displayName = string.Empty;
    private string _shortcut = string.Empty;
    private string _defaultValueText = string.Empty;
    private double _actionValue;
    private string? _notes;
    private string _status = string.Empty;
    private bool _isEnabled;
    private TrainerAvailability _availability;
    private string _applyLabel = "Apply";
    private Func<string, Task>? _executeAsync;
    private Action<string, long>? _setValue;

    private LocalizedTrainerFeature(TrainerFeatureDefinition definition)
    {
        Id = definition.Id;
        Kind = definition.Kind;
        DefaultValue = definition.DefaultValue;
        ExecuteCommand = new AsyncCommand(this);
    }

    public string Id { get; }
    public TrainerFeatureKind Kind { get; }
    public long? DefaultValue { get; }
    public string Group { get => _group; private set => SetField(ref _group, value); }
    public string Scope { get => _scope; private set => SetField(ref _scope, value); }
    public string DisplayName { get => _displayName; private set => SetField(ref _displayName, value); }
    public string Shortcut { get => _shortcut; private set => SetField(ref _shortcut, value); }
    public string DefaultValueText { get => _defaultValueText; private set => SetField(ref _defaultValueText, value); }
    public string? Notes { get => _notes; private set => SetField(ref _notes, value); }
    public string Status { get => _status; private set => SetField(ref _status, value); }
    public bool IsEnabled { get => _isEnabled; private set => SetField(ref _isEnabled, value); }
    public TrainerAvailability Availability
    {
        get => _availability;
        private set
        {
            if (SetField(ref _availability, value))
            {
                OnPropertyChanged(nameof(IsAvailable));
                (ExecuteCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public double ActionValue
    {
        get => _actionValue;
        set
        {
            var normalized = Math.Clamp(Math.Round(value), 1d, 8_000_000d);
            if (SetField(ref _actionValue, normalized))
            {
                DefaultValueText = ((long)normalized).ToString();
                _setValue?.Invoke(Id, (long)normalized);
            }
        }
    }

    public string ApplyLabel { get => _applyLabel; private set => SetField(ref _applyLabel, value); }
    public Visibility ActionEditorVisibility => Kind == TrainerFeatureKind.ValueAction ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ToggleVisibility => Kind == TrainerFeatureKind.Toggle ? Visibility.Visible : Visibility.Collapsed;
    public bool IsAvailable => Availability == TrainerAvailability.Available;
    public ICommand ExecuteCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ConfigureActions(Func<string, Task> executeAsync, Action<string, long> setValue)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _setValue = setValue ?? throw new ArgumentNullException(nameof(setValue));
        (ExecuteCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }

    public void UpdateFrom(
        TrainerFeatureDefinition definition,
        ILocalizationService localization,
        TrainerFeatureState? state = null,
        string? status = null,
        long? configuredActionValue = null)
    {
        var groupKey = definition.Group switch
        {
            "Player" => "TrainerGroup_Player",
            "Units" => "TrainerGroup_Units",
            "Cities" => "TrainerGroup_Cities",
            "AI" => "TrainerGroup_AI",
            "Combat" => "TrainerGroup_Combat",
            _ => string.Empty,
        };

        Group = string.IsNullOrEmpty(groupKey) ? definition.Group : localization.GetString(groupKey);
        Scope = definition.Id.StartsWith("ai.", StringComparison.Ordinal)
            ? localization.GetString("TrainerGroup_AI")
            : definition.Id.StartsWith("combat.", StringComparison.Ordinal)
                ? localization.GetString("TrainerGroup_Combat")
                : localization.GetString("TrainerGroup_Player");

        var featureKey = "TrainerFeature_" + definition.Id.Replace('.', '_').Replace('-', '_');
        var displayName = localization.GetString(featureKey);
        DisplayName = displayName.StartsWith('!') && displayName.EndsWith('!') ? definition.DisplayName : displayName;
        Shortcut = definition.Shortcut;
        Notes = definition.Notes;
        var apply = localization.GetString("Trainer_ApplyButton.Content");
        ApplyLabel = apply.StartsWith('!') && apply.EndsWith('!') ? "Apply" : apply;

        Availability = state?.Availability ?? TrainerAvailability.SignaturePending;
        IsEnabled = state?.IsEnabled ?? false;
        Status = IsTransientMatchReadinessFailure(state)
            ? localization.GetString("Trainer_StatusWaiting.Text")
            : status ?? state?.StatusMessage ?? string.Empty;

        if (Kind == TrainerFeatureKind.ValueAction)
        {
            var nextValue = configuredActionValue ?? definition.DefaultValue ?? 1;
            if (Math.Abs(_actionValue - nextValue) > double.Epsilon)
            {
                _actionValue = nextValue;
                DefaultValueText = nextValue.ToString();
                OnPropertyChanged(nameof(ActionValue));
            }
        }
    }

    public static LocalizedTrainerFeature From(
        TrainerFeatureDefinition definition,
        ILocalizationService localization,
        TrainerFeatureState? state = null,
        string? status = null,
        long? configuredActionValue = null)
    {
        var item = new LocalizedTrainerFeature(definition);
        item.UpdateFrom(definition, localization, state, status, configuredActionValue);
        return item;
    }

    private static bool IsTransientMatchReadinessFailure(TrainerFeatureState? state) =>
        state?.Availability == TrainerAvailability.Error
        && state.StatusMessage is { } message
        && (message.Contains("player manager pointer is null", StringComparison.OrdinalIgnoreCase)
            || message.Contains("game context root is unavailable", StringComparison.OrdinalIgnoreCase)
            || message.Contains("local player", StringComparison.OrdinalIgnoreCase));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class AsyncCommand : ICommand
    {
        private readonly LocalizedTrainerFeature _owner;
        private bool _running;

        public AsyncCommand(LocalizedTrainerFeature owner) => _owner = owner;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => !_running && _owner.IsAvailable && _owner._executeAsync is not null;

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter) || _owner._executeAsync is null) return;
            _running = true;
            RaiseCanExecuteChanged();
            try { await _owner._executeAsync(_owner.Id); }
            finally
            {
                _running = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}