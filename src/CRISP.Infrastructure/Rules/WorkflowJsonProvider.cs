using CRISP.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
namespace CRISP.Infrastructure.Rules;
public sealed class WorkflowJsonProvider(
IFrameworkRepository repository,
IMemoryCache cache) : IWorkflowJsonProvider
 {
    private static string DefinitionKey(string workflowCode, int versionNo) =>
    $"CRISP:WorkflowDefinition:{workflowCode}:{versionNo}";
    private static string ValueKey(int frameworkId) =>
    $"CRISP:FrameworkValues:{frameworkId}";
    public async Task<string?> GetDefinitionJsonAsync(string workflowCode, int versionNo, CancellationToken ct = default)
     {
        var key = DefinitionKey(workflowCode, versionNo);
        if (cache.TryGetValue(key, out string? cached) && !string.IsNullOrWhiteSpace(cached))
        return cached;
        var version = await repository.GetVersionAsync(workflowCode, versionNo, ct);
        if (version is null || string.IsNullOrWhiteSpace(version.JsonContent))
        return null;
        cache.Set(key, version.JsonContent, new MemoryCacheEntryOptions
         {
            SlidingExpiration = TimeSpan.FromMinutes(30),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)
        });
        return version.JsonContent;
    }
    public async Task<string?> GetValueJsonAsync(int frameworkId, CancellationToken ct = default)
     {
        var key = ValueKey(frameworkId);
        if (cache.TryGetValue(key, out string? cached) && !string.IsNullOrWhiteSpace(cached))
        return cached;
        var json = await repository.GetFrameworkValueJsonAsync(frameworkId, ct);
        if (!string.IsNullOrWhiteSpace(json))
         {
            cache.Set(key, json, new MemoryCacheEntryOptions
             {
                SlidingExpiration = TimeSpan.FromMinutes(30),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)
            });
        }
        return json;
    }
    public void SetDefinitionJson(string workflowCode, int versionNo, string json)
     {
        cache.Set(DefinitionKey(workflowCode, versionNo), json, new MemoryCacheEntryOptions
         {
            SlidingExpiration = TimeSpan.FromMinutes(30),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)
        });
    }
    public void SetValueJson(int frameworkId, string json)
     {
        cache.Set(ValueKey(frameworkId), json, new MemoryCacheEntryOptions
         {
            SlidingExpiration = TimeSpan.FromMinutes(30),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)
        });
    }
    public void InvalidateDefinition(string workflowCode, int versionNo) =>
    cache.Remove(DefinitionKey(workflowCode, versionNo));
    public void InvalidateValue(int frameworkId) =>
    cache.Remove(ValueKey(frameworkId));
}
