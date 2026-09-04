namespace OmniBrille.Core;

public enum FileActivationResult
{
    Opened,
    NotFound,
    OutsideAccessRoot,
    ReparsePointBlocked,
    HighRiskTypeBlocked,
    UnsupportedAuthority,
    Failed,
}

public interface IFileActivationService
{
    public Task<FileActivationResult> OpenAsync(
        string accessRoot,
        string path,
        CancellationToken cancellationToken = default);
}
