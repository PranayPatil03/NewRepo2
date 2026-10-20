using CRISP.Application.DTOs;
using CRISP.Domain.Entities;
namespace CRISP.Application.Interfaces;

public interface IFrameworkRepository
{
    Task<IReadOnlyList<WorkflowDto>> GetWorkflowsAsync(CancellationToken ct = default);
    Task<WorkflowDto> CreateWorkflowAsync(CreateWorkflowRequest request, string skeletonJson, CancellationToken ct = default);
    Task<WorkflowContextDto?> GetWorkflowContextAsync(string workflowCode, CancellationToken ct = default);
    Task<IReadOnlyList<EntityDto>> SearchEntitiesAsync(string? search, CancellationToken ct = default);
    Task<IReadOnlyList<FrameworkLookupDto>> GetFrameworkOptionsAsync(int entityId, CancellationToken ct = default);
    Task<IReadOnlyList<IndustrySegmentDto>> GetIndustrySegmentsAsync(string workflowCode, CancellationToken ct = default);
    Task<IReadOnlyList<FrameworkStepDto>> GetStepsAsync(string workflowCode, CancellationToken ct = default);
    Task<IReadOnlyList<FrameworkListDto>> GetFrameworksAsync(CancellationToken ct = default);
    Task<ParameterDto?> GetParameterAsync(int parameterId, CancellationToken ct = default);
    Task<IReadOnlyList<ParameterDto>> GetParametersAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<VersionDto>> GetVersionsAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<IndustrySegmentDto>> GetAllIndustriesAsync(
    CancellationToken ct = default);
    Task<VersionDto?> GetVersionAsync(
    string code,
    int versionNo,
    CancellationToken ct = default);


    Task<FrameworkDto?> GetFrameworkAsync(int id, int? versionNo = null, CancellationToken ct = default);
    Task<FrameworkDto?> GetFrameworkShellAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<FrameworkParameterDto>> GetFrameworkParameterValuesAsync(int id, CancellationToken ct = default);
    Task<string?> GetFrameworkValueJsonAsync(int frameworkId, CancellationToken ct = default);
    Task<string?> GetFrameworkValueJsonAsync(int frameworkId, int versionNo, CancellationToken ct = default);
    Task SaveFrameworkValueJsonAsync(int frameworkId, int versionNo, string jsonContent, CancellationToken ct = default);
    Task ClearFrameworkValueJsonAsync(int frameworkId, CancellationToken ct = default);
    Task<FrameworkDto> SaveFrameworkAsync(FrameworkDto framework, CancellationToken ct = default);
    Task<VersionDto> SaveVersionAsync(SaveVersionRequest request, CancellationToken ct = default);
    Task<WorkflowDefinitionDto?> GetWorkflowDefinitionAsync(string workflowCode, CancellationToken ct = default);
    Task<VersionDto> SaveWorkflowDefinitionAsync(SaveWorkflowDefinitionRequest request, string jsonContent, CancellationToken ct = default);

    Task<IReadOnlyList<ParameterDto>> GetParameterMastersAsync(
    CancellationToken ct = default);

    Task<ParameterDto> SaveParameterMasterAsync(
        ParameterDto request,
        CancellationToken ct = default);

    Task<WorkflowJsonVersion?> GetWorkflowVersionAsync(
    string workflowCode,
    int versionNo,
    CancellationToken ct = default);

    Task<IReadOnlyList<IndustryParameterMappingDto>>
        GetIndustryParameterMappingsAsync(
            string workflowCode,
            int versionNo,
            int industryId,
            CancellationToken ct = default);

    Task SaveIndustryParameterMappingsAsync(
        int workflowJsonVersionId,
        int industryId,
        IReadOnlyList<IndustryParameterMappingDto> mappings,
        CancellationToken ct = default);

    Task<WorkflowDefinitionDto?>
    BuildWorkflowDefinitionAsync(
        string workflowCode,
        int versionNo,
        CancellationToken ct = default);

    Task<WorkflowJsonVersion?>
        PublishVersionAsync(
            string workflowCode,
            int versionNo,
            string jsonContent,
            CancellationToken ct = default);

    Task<ScoreConfigurationDto?> GetScoreConfigurationAsync(
    int industryParameterMappingId,
    CancellationToken ct = default);

    Task SaveScoreConfigurationAsync(
        ScoreConfigurationDto request,
        CancellationToken ct = default);
}