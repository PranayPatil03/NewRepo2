namespace CRISP.Application.Interfaces;
public interface IWorkflowJsonProvider
 {
    Task<string?> GetDefinitionJsonAsync(string workflowCode, int versionNo, CancellationToken ct = default);
    Task<string?> GetValueJsonAsync(int frameworkId, CancellationToken ct = default);
    void SetDefinitionJson(string workflowCode, int versionNo, string json);
    void SetValueJson(int frameworkId, string json);
    void InvalidateDefinition(string workflowCode, int versionNo);
    void InvalidateValue(int frameworkId);
}
