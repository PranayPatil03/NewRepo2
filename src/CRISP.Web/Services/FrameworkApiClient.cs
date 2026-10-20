using CRISP.Application.DTOs;
using CRISP.Web.Models;
using System.Net.Http.Json;
namespace CRISP.Web.Services;

public sealed class FrameworkApiClient(IHttpClientFactory factory, IReadOnlyList<string> baseUrls)
{
    private readonly IReadOnlyList<string> _baseUrls = baseUrls
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .Select(x => x.EndsWith('/') ? x : x + "/")
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();
    private async Task<T?> GetAsync<T>(
    string relativeUrl,
    CancellationToken ct = default)
    {
        Exception? last = null;

        foreach (var baseUrl in _baseUrls)
        {
            var client = factory.CreateClient("CRISP.Api");
            client.BaseAddress = new Uri(baseUrl);

            var requestUrl = new Uri(client.BaseAddress, relativeUrl);

            try
            {
                using var response = await client.GetAsync(relativeUrl, ct);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content
                        .ReadFromJsonAsync<T>(cancellationToken: ct);
                }

                var body = await response.Content.ReadAsStringAsync(ct);

                var error = new HttpRequestException(
                    $"GET {requestUrl} failed. " +
                    $"HTTP {(int)response.StatusCode} " +
                    $"({response.StatusCode}). Response: {body}",
                    null,
                    response.StatusCode);

                // A 404 or another HTTP error is not a connection failure.
                // Report it immediately instead of trying another base URL.
                if ((int)response.StatusCode >= 400)
                    throw error;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                last = new TimeoutException(
                    $"API request timed out: {requestUrl}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                last = ex;
            }
        }

        throw last ?? new HttpRequestException(
            "No configured CRISP API endpoint is reachable.");
    }
    private async Task<T?> PostAsync<T>(string relativeUrl, object request, CancellationToken ct = default)
    {
        Exception? last = null;
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var client = factory.CreateClient("CRISP.Api");
                client.BaseAddress = new Uri(baseUrl);
                using var response = await client.PostAsJsonAsync(relativeUrl, request, ct);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
                if ((int)response.StatusCode >= 400)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"API {response.StatusCode}: {body}");
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                last = new TimeoutException($"API request timed out: {baseUrl}");
            }
            catch (HttpRequestException ex)
            {
                last = ex;
            }
        }
        throw last ?? new HttpRequestException("No configured CRISP API endpoint is reachable.");
    }
    public async Task<List<WorkflowDto>> GetWorkflowsAsync(CancellationToken ct = default)
    => await GetAsync<List<WorkflowDto>>("api/framework/workflows", ct) ?? [];
    public async Task<WorkflowDto?> CreateWorkflowAsync(
    CreateWorkflowRequest request,
    CancellationToken ct = default)
    {
        return await PostAsync<WorkflowDto>(
        "api/framework/workflows",
        request,
        ct);
    }
    public async Task<WorkflowContextDto?> GetWorkflowContextAsync(string workflowCode, CancellationToken ct = default)
    => await GetAsync<WorkflowContextDto>($"api/framework/context/{Uri.EscapeDataString(workflowCode)}", ct);
    public async Task<List<EntityDto>> SearchEntitiesAsync(string? search, CancellationToken ct = default)
    {
        var term = search?.Trim() ?? string.Empty;
        var url = $"api/framework/entities?search={Uri.EscapeDataString(term)}";
        return await GetAsync<List<EntityDto>>(url, ct) ?? [];
    }
    public async Task<List<FrameworkLookupDto>> GetFrameworkOptionsAsync(int entityId, CancellationToken ct = default)
    {
        var url = $"api/framework/entities/{entityId}/frameworks";
        return await GetAsync<List<FrameworkLookupDto>>(url, ct) ?? [];
    }
    public async Task<List<FrameworkStepDto>> GetStepsAsync(string workflowCode, CancellationToken ct = default)
    => await GetAsync<List<FrameworkStepDto>>($"api/framework/{Uri.EscapeDataString(workflowCode)}/steps", ct) ?? [];
    public async Task<List<IndustrySegmentDto>> GetIndustrySegmentsAsync(string workflowCode, CancellationToken ct = default)
    => await GetAsync<List<IndustrySegmentDto>>($"api/framework/{Uri.EscapeDataString(workflowCode)}/industries", ct) ?? [];
    public async Task<List<FrameworkListDto>> GetFrameworksAsync(CancellationToken ct = default)
    => await GetAsync<List<FrameworkListDto>>("api/framework/instances", ct) ?? [];
    public async Task<FrameworkDto?> GetFrameworkAsync(int id, int? versionNo = null, CancellationToken ct = default)
    {
        var url = $"api/framework/instances/{id}" + (versionNo.HasValue ? $"?versionNo={versionNo.Value}" : string.Empty);
        return await GetAsync<FrameworkDto>(url, ct);
    }
    public async Task<FrameworkDto?> SaveFrameworkAsync(FrameworkDto request, CancellationToken ct = default)
    => await PostAsync<FrameworkDto>("api/framework/instances", request, ct);
    public async Task<List<ParameterDto>> GetParametersAsync(string code, CancellationToken ct = default)
    => await GetAsync<List<ParameterDto>>($"api/framework/{Uri.EscapeDataString(code)}/parameters", ct) ?? [];

    public async Task<ParameterDto?> GetParameterAsync(int parameterId, CancellationToken ct = default)
    => await GetAsync<ParameterDto>($"api/framework/parameters/by-id/{parameterId}", ct);

    public async Task<List<ParameterDto>> GetParameterMastersAsync(CancellationToken ct = default)
        => await GetAsync<List<ParameterDto>>("api/framework/parameter-masters", ct) ?? [];

    public async Task<ParameterDto?> SaveParameterMasterAsync(ParameterDto request, CancellationToken ct = default)
        => await PostAsync<ParameterDto>("api/framework/parameter-masters", request, ct);
    public async Task<List<VersionDto>> GetVersionsAsync(string code, CancellationToken ct = default)
    => await GetAsync<List<VersionDto>>($"api/framework/{Uri.EscapeDataString(code)}/versions", ct) ?? [];
    public async Task<ApiVersionDetail?> GetVersionAsync(string code, int version, CancellationToken ct = default)
    => await GetAsync<ApiVersionDetail>($"api/framework/{Uri.EscapeDataString(code)}/versions/{version}", ct);
    public async Task<CalculateScoreResponse?> CalculateScoreAsync(CalculateScoreRequest request, CancellationToken ct = default)
    => await PostAsync<CalculateScoreResponse>("api/framework/calculate-score", request, ct);
    public async Task<EvaluateResponse?> EvaluateAsync(EvaluateRequest request, CancellationToken ct = default)
    => await PostAsync<EvaluateResponse>("api/framework/evaluate", request, ct);
    public async Task<VersionDto?> SaveVersionAsync(SaveVersionRequest request, CancellationToken ct = default)
    => await PostAsync<VersionDto>("api/framework/versions", request, ct);
    public async Task<WorkflowDefinitionDto?> GetWorkflowDefinitionAsync(string code, CancellationToken ct = default)
    => await GetAsync<WorkflowDefinitionDto>($"api/framework/{Uri.EscapeDataString(code)}/definition", ct);
    public async Task<VersionDto?> SaveWorkflowDefinitionAsync(SaveWorkflowDefinitionRequest request, CancellationToken ct = default)
    => await PostAsync<VersionDto>("api/framework/definition", request, ct);

    public async Task<List<IndustryParameterMappingDto>> GetIndustryParameterMappingsAsync(string workflowCode, int versionNo, int industryId, CancellationToken ct = default)
        => await GetAsync<List<IndustryParameterMappingDto>>($"api/framework/{Uri.EscapeDataString(workflowCode)}/versions/{versionNo}/industries/{industryId}/mappings", ct) ?? [];

    public async Task<bool> SaveIndustryParameterMappingsAsync(SaveIndustryParameterMappingsRequest request, CancellationToken ct = default)
        => await PostAsync<bool>($"api/framework/{Uri.EscapeDataString(request.WorkflowCode)}/versions/{request.VersionNo}/industries/{request.IndustryDetailsId}/mappings", request, ct);

    public async Task<VersionDto?> PublishVersionAsync(string workflowCode, int versionNo, CancellationToken ct = default)
        => await PostAsync<VersionDto>($"api/framework/{Uri.EscapeDataString(workflowCode)}/versions/{versionNo}/publish", new { WorkflowCode = workflowCode, VersionNo = versionNo }, ct);

    public async Task<List<IndustrySegmentDto>>
    GetAllIndustriesAsync(
        CancellationToken ct = default)
    {
        return await GetAsync<List<IndustrySegmentDto>>(
            "api/framework/industries",
            ct) ?? [];
    }

    public async Task<ScoreConfigurationDto?>
    GetScoreConfigurationAsync(
        int mappingId,
        CancellationToken ct = default)
    {
        return await GetAsync<ScoreConfigurationDto>(
            $"api/framework/score-configurations/{mappingId}",
            ct);
    }

    public async Task<bool>
    SaveScoreConfigurationAsync(
        ScoreConfigurationDto request,
        CancellationToken ct = default)
    {
        return await PostAsync<bool>(
            "api/framework/score-configurations",
            request,
            ct);
    }
}