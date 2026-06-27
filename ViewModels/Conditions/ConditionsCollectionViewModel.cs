using System.Collections.Generic;

namespace DiskSpaceTracker.ViewModels.Conditions;

/// <summary>
/// Holds the seven condition view-models for one trigger tab. Exposed as both individual
/// properties (used by XAML bindings for direct field access) and as a flat
/// <see cref="AllConditions"/> sequence (used by the engine to iterate enabled conditions).
/// </summary>
public sealed class ConditionsCollectionViewModel
{
    public ConditionsCollectionViewModel(
        WaitTimeConditionViewModel waitTime,
        ExecuteConditionViewModel execute,
        FileExistConditionViewModel fileExist,
        FileLockedConditionViewModel fileLocked,
        ProcessExistConditionViewModel processExist,
        RegistryConditionViewModel registry,
        TextFileConditionViewModel textFile)
    {
        WaitTime = waitTime;
        Execute = execute;
        FileExist = fileExist;
        FileLocked = fileLocked;
        ProcessExist = processExist;
        Registry = registry;
        TextFile = textFile;

        AllConditions = new ConditionViewModelBase[]
        {
            waitTime, execute, fileExist, fileLocked, processExist, registry, textFile,
        };
    }

    public WaitTimeConditionViewModel WaitTime { get; }
    public ExecuteConditionViewModel Execute { get; }
    public FileExistConditionViewModel FileExist { get; }
    public FileLockedConditionViewModel FileLocked { get; }
    public ProcessExistConditionViewModel ProcessExist { get; }
    public RegistryConditionViewModel Registry { get; }
    public TextFileConditionViewModel TextFile { get; }

    public IReadOnlyList<ConditionViewModelBase> AllConditions { get; }
}
