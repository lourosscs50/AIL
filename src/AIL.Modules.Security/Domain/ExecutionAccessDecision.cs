namespace AIL.Modules.Security.Domain;

public sealed record ExecutionAccessDecision(bool IsAllowed, string? Reason = null)
{
    public static ExecutionAccessDecision Allow() => new(true);
    public static ExecutionAccessDecision Deny(string reason) => new(false, reason);
}
