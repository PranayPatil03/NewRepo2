using CRISP.Application.DTOs;
using CRISP.Domain.Entities;
namespace CRISP.Application.Interfaces;

public interface IFrameworkService
{
    Task<IReadOnlyList<WorkflowDto>> GetWorkflowsAsync(CancellationToken ct = default);
    Task<WorkflowDto> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default);
    Task<WorkflowContextDto?> GetWorkflowContextAsync(string workflowCode, CancellationToken ct = default);
    Task<IReadOnlyList<EntityDto>> SearchEntitiesAsync(string? search, CancellationToken ct = default);
    Task<IReadOnlyList<FrameworkLookupDto>> GetFrameworkOptionsAsync(int entityId, CancellationToken ct = default);
    Task<IReadOnlyList<IndustrySegmentDto>> GetIndustrySegmentsAsync(string workflowCode, CancellationToken ct = default);
    Task<IReadOnlyList<FrameworkStepDto>> GetStepsAsync(string workflowCode, CancellationToken ct = default);
    Task<IReadOnlyList<FrameworkListDto>> GetFrameworksAsync(CancellationToken ct = default);
    Task<FrameworkDto?> GetFrameworkAsync(int id, int? versionNo = null, CancellationToken ct = default);
    Task<IReadOnlyList<ParameterDto>> GetParametersAsync(string workflowCode, CancellationToken ct = default);
    Task<ParameterDto?> GetParameterAsync(int parameterId, CancellationToken ct = default);
    Task<CalculateScoreResponse?> CalculateScoreAsync(int parameterId, decimal? value, CancellationToken ct = default);
    Task<IReadOnlyList<VersionDto>> GetVersionsAsync(string workflowCode, CancellationToken ct = default);
    Task<VersionDto?> GetVersionAsync(
    string workflowCode,
    int versionNo,
    CancellationToken ct = default);
    Task<FrameworkDto> SaveFrameworkAsync(FrameworkDto framework, CancellationToken ct = default);
    Task<VersionDto> SaveVersionAsync(SaveVersionRequest request, CancellationToken ct = default);
    Task<WorkflowDefinitionDto?> GetWorkflowDefinitionAsync(string workflowCode, CancellationToken ct = default);
    Task<VersionDto> SaveWorkflowDefinitionAsync(SaveWorkflowDefinitionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ParameterDto>> GetParameterMastersAsync(
    CancellationToken ct = default);

    Task<ParameterDto> SaveParameterMasterAsync(
        ParameterDto request,
        CancellationToken ct = default);


    Task<IReadOnlyList<IndustryParameterMappingDto>>
    GetIndustryParameterMappingsAsync(
        string workflowCode,
        int versionNo,
        int industryId,
        CancellationToken ct = default);

    Task<bool> SaveIndustryParameterMappingsAsync(
        SaveIndustryParameterMappingsRequest request,
        CancellationToken ct = default);

    Task<VersionDto> PublishVersionAsync(
        string workflowCode,
        int versionNo,
        CancellationToken ct = default);

    Task<IReadOnlyList<IndustrySegmentDto>> GetAllIndustriesAsync(
    CancellationToken ct = default);


    Task<ScoreConfigurationDto?> GetScoreConfigurationAsync(
    int industryParameterMappingId,
    CancellationToken ct = default);

    Task SaveScoreConfigurationAsync(
        ScoreConfigurationDto request,
        CancellationToken ct = default);


}