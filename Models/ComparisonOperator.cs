namespace DiskSpaceTracker.Models;

/// <summary>
/// Text-file line comparison operators exposed to the user via six radio buttons:
/// =, !=, ≠ (NotEqual), !* (NotContains), Starts, Ends.
/// </summary>
public enum ComparisonOperator
{
    Equal = 0,
    /// <summary>UI label is "!=", which maps to "Contains".</summary>
    Contains = 1,
    NotEqual = 2,
    NotContains = 3,
    StartsWith = 4,
    EndsWith = 5,
}

/// <summary>
/// Normalized form of a comparison: a core operator (Equal/Contains/StartsWith/EndsWith)
/// and a negation flag. Used by the analyzer to keep its inner loop simple.
/// </summary>
public readonly record struct NormalizedComparison(ComparisonOperator CoreOp, bool Negated)
{
    /// <summary>Resolve a user-facing operator into its normalized core+negate pair.</summary>
    public static NormalizedComparison From(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => new(ComparisonOperator.Equal, false),
        ComparisonOperator.NotEqual => new(ComparisonOperator.Equal, true),
        ComparisonOperator.Contains => new(ComparisonOperator.Contains, false),
        ComparisonOperator.NotContains => new(ComparisonOperator.Contains, true),
        ComparisonOperator.StartsWith => new(ComparisonOperator.StartsWith, false),
        ComparisonOperator.EndsWith => new(ComparisonOperator.EndsWith, false),
        _ => new(ComparisonOperator.Equal, false),
    };
}
