namespace CSharpFunctionalExtensions.Analyzers.UseResultValueWithoutCheck;

internal class WalkerResult
{
    public bool AccessedValue { get; set; }
    public CheckResult CheckResult { get; set; } = CheckResult.Unchecked;
    public bool Terminated { get; set; }

    public bool CorrectUsage =>
        (AccessedValue && CheckResult == CheckResult.CheckedSuccess) || 
        (CheckResult == CheckResult.CheckedFailure && Terminated && !AccessedValue);

    public void Reset()
    {
        AccessedValue = false;
        CheckResult = CheckResult.Unchecked;
        Terminated = false;
    }

    public static WalkerResult Default => new WalkerResult();
}

internal enum CheckResult
{
    Unchecked,
    CheckedSuccess,
    CheckedFailure,
    AccesedValue
}
