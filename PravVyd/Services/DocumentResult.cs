namespace PravVyd.Services;

public enum DocumentOutcome
{
    Success,
    NoSelection,
    Error,
}

public sealed record DocumentResult(DocumentOutcome Outcome, string? Path = null, string? Error = null)
{
    public static DocumentResult Ok(string path) => new(DocumentOutcome.Success, Path: path);

    public static DocumentResult NoSelection() => new(DocumentOutcome.NoSelection);

    public static DocumentResult Failure(string error) => new(DocumentOutcome.Error, Error: error);
}
