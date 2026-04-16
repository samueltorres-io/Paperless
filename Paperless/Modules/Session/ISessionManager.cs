namespace Paperless.Modules.Session;

public interface ISessionManager
{
    bool IsExpired { get; }
    bool HasContext  { get; }
    string Summary   { get; }
    void UpdateSummary(string summary);
    void Touch();
    void Reset();
}