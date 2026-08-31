namespace GameLoopOptimizer.Core;

public record OptimizationsChangedMessage;

public record SystemDataRefreshedMessage;

public record KeymapsUpdatedMessage;

public record BackupRestoredMessage;

public record VmSettingsChangedMessage;

public record StatusNotificationMessage(string Message, bool IsError = false);
