using CRISP.Application.DTOs;
using CRISP.Application.Interfaces;
using CRISP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
namespace CRISP.Api.Controllers;

[ApiController]
[Route("api/framework")]
public sealed class FrameworkController : ControllerBase
{
    private readonly IFrameworkService _frameworkService;
    private readonly IRuleEvaluationService _evaluationService;
    private readonly CrispDbContext _db;
    public FrameworkController(
    IFrameworkService frameworkService,
    IRuleEvaluationService evaluationService, CrispDbContext db)
    {
        _frameworkService = frameworkService;
        _evaluationService = evaluationService;
        _db = db;
    }
    [HttpGet("workflows")]
    public async Task<ActionResult<IReadOnlyList<WorkflowDto>>> Workflows(
    CancellationToken ct)
    {
        return Ok(await _frameworkService.GetWorkflowsAsync(ct));
    }
    [HttpPost("workflows")]
    public async Task<ActionResult<WorkflowDto>> CreateWorkflow(
    CreateWorkflowRequest request,
    CancellationToken ct)
    {
        return Ok(await _frameworkService.CreateWorkflowAsync(request, ct));
    }
    [HttpGet("context/{code}")]
    public async Task<IActionResult> WorkflowContext(
    string code,
    CancellationToken ct)
    {
        var context = await _frameworkService.GetWorkflowContextAsync(code, ct);
        return context is null ? NotFound() : Ok(context);
    }
    [HttpGet("entities")]
    public async Task<ActionResult<IReadOnlyList<EntityDto>>> Entities(
    [FromQuery] string? search,
    CancellationToken ct)
    {
        return Ok(await _frameworkService.SearchEntitiesAsync(search, ct));
    }
    [HttpGet("entities/{entityId:int}/frameworks")]
    public async Task<ActionResult<IReadOnlyList<FrameworkLookupDto>>> FrameworkOptions(
    int entityId,
    CancellationToken ct)
    {
        return Ok(await _frameworkService.GetFrameworkOptionsAsync(entityId, ct));
    }
    [HttpGet("{code}/steps")]
    public async Task<ActionResult<IReadOnlyList<FrameworkStepDto>>> Steps(
    string code,
    CancellationToken ct)
    {
        return Ok(await _frameworkService.GetStepsAsync(code, ct));
    }
    [HttpGet("{code}/industries")]
    public async Task<ActionResult<IReadOnlyList<IndustrySegmentDto>>> Industries(
    string code,
    CancellationToken ct)
    {
        return Ok(await _frameworkService.GetIndustrySegmentsAsync(code, ct));
    }
    [HttpGet("instances")]
    public async Task<ActionResult<IReadOnlyList<FrameworkListDto>>> Instances(
    CancellationToken ct)
    {
        return Ok(await _frameworkService.GetFrameworksAsync(ct));
    }

    [HttpGet("parameters/by-id/{parameterId:int}")]
    public async Task<ActionResult<ParameterDto>> ParameterById(int parameterId, CancellationToken ct)
    {
        var parameter = await _frameworkService.GetParameterAsync(parameterId, ct);
        return parameter is null ? NotFound() : Ok(parameter);
    }

    [HttpGet("{code}/parameters")]
    public async Task<ActionResult<IReadOnlyList<ParameterDto>>> Parameters(
    string code,
    CancellationToken ct)
    {
        return Ok(await _frameworkService.GetParametersAsync(code, ct));
    }
    [HttpGet("{code}/versions")]
    public async Task<ActionResult<IReadOnlyList<VersionDto>>> Versions(
    string code,
    CancellationToken ct)
    {
        return Ok(await _frameworkService.GetVersionsAsync(code, ct));
    }

    [HttpGet("{code}/versions/{versionNo:int}")]
    public async Task<ActionResult<VersionDto>> Version(
    string code,
    int versionNo,
    CancellationToken ct)
    {
        var version = await _frameworkService.GetVersionAsync(
            code,
            versionNo,
            ct);

        if (version is null)
        {
            return NotFound();
        }

        return Ok(version);
    }

    [HttpGet("instances/{id:int}")]
    public async Task<IActionResult> Instance(
    int id,
    [FromQuery] int? versionNo,
    CancellationToken ct)
    {
        var framework = await _frameworkService.GetFrameworkAsync(id, versionNo, ct);
        return framework is null ? NotFound() : Ok(framework);
    }
    [HttpPost("instances")]
    public async Task<ActionResult<FrameworkDto>> SaveInstance(
    FrameworkDto model,
    CancellationToken ct)
    {
        return Ok(await _frameworkService.SaveFrameworkAsync(model, ct));
    }
    [HttpPost("calculate-score")]
    public async Task<IActionResult> CalculateScore(
    CalculateScoreRequest request,
    CancellationToken ct)
    {
        var result = await _frameworkService.CalculateScoreAsync(
        request.ParameterId,
        request.Value,
        ct);
        return result is null ? NotFound() : Ok(result);
    }
    [HttpPost("evaluate")]
    public async Task<ActionResult<EvaluateResponse>> Evaluate(
    EvaluateRequest request,
    CancellationToken ct)
    {
        return Ok(await _evaluationService.EvaluateAsync(request, ct));
    }
    [HttpPost("versions")]
    public async Task<ActionResult<VersionDto>> SaveVersion(
    SaveVersionRequest request,
    CancellationToken ct)
    {
        return Ok(await _frameworkService.SaveVersionAsync(request, ct));
    }
    [HttpGet("{code}/definition")]
    public async Task<IActionResult> Definition(
    string code,
    CancellationToken ct)
    {
        var definition = await _frameworkService.GetWorkflowDefinitionAsync(code, ct);
        return definition is null ? NotFound() : Ok(definition);
    }
    [HttpPost("definition")]
    public async Task<ActionResult<VersionDto>> SaveDefinition(
    SaveWorkflowDefinitionRequest request,
    CancellationToken ct)
    {
        return Ok(await _frameworkService.SaveWorkflowDefinitionAsync(request, ct));
    }

    [HttpGet("{code}/versions/{versionNo:int}/industries/{industryId:int}/mappings")]
    public async Task<
      ActionResult<IReadOnlyList<IndustryParameterMappingDto>>>
      IndustryParameterMappings(
          string code,
          int versionNo,
          int industryId,
          CancellationToken ct)
    {
        var result =
            await _frameworkService
                .GetIndustryParameterMappingsAsync(
                    code,
                    versionNo,
                    industryId,
                    ct);

        return Ok(result);
    }

    [HttpPost("{code}/versions/{versionNo:int}/industries/{industryId:int}/mappings")]
    public async Task<ActionResult<bool>>
    SaveIndustryParameterMappings(
        string code,
        int versionNo,
        int industryId,
        [FromBody] SaveIndustryParameterMappingsRequest request,
        CancellationToken ct)
    {
        request.WorkflowCode = code;
        request.VersionNo = versionNo;
        request.IndustryDetailsId = industryId;

        var result =
            await _frameworkService
                .SaveIndustryParameterMappingsAsync(
                    request,
                    ct);

        return Ok(result);
    }

    [HttpGet("parameter-masters")]
    public async Task<ActionResult<IReadOnlyList<ParameterDto>>> ParameterMasters(
    CancellationToken ct)
    {
        return Ok(
            await _frameworkService.GetParameterMastersAsync(ct));
    }

    [HttpPost("parameter-masters")]
    public async Task<ActionResult<ParameterDto>> SaveParameterMaster(
        [FromBody] ParameterDto request,
        CancellationToken ct)
    {
        return Ok(
            await _frameworkService.SaveParameterMasterAsync(request, ct));
    }

    [HttpPost("{code}/versions/{versionNo:int}/publish")]
    public async Task<ActionResult<VersionDto>> PublishVersion(
    string code,
    int versionNo,
    CancellationToken ct)
    {
        var result =
            await _frameworkService.PublishVersionAsync(
                code,
                versionNo,
                ct);

        return Ok(result);
    }

    [HttpGet("industries")]
    public async Task<
    ActionResult<IReadOnlyList<IndustrySegmentDto>>>
    AllIndustries(CancellationToken ct)
    {
        return Ok(
            await _frameworkService.GetAllIndustriesAsync(ct));
    }

    [HttpGet("score-configurations/{mappingId:int}")]
    public async Task<ActionResult<ScoreConfigurationDto>>
    GetScoreConfiguration(
        int mappingId,
        CancellationToken ct)
    {
        var result =
            await _frameworkService
                .GetScoreConfigurationAsync(
                    mappingId,
                    ct);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpPost("score-configurations")]
    public async Task<ActionResult<bool>>
    SaveScoreConfiguration(
        [FromBody] ScoreConfigurationDto request,
        CancellationToken ct)
    {
        await _frameworkService
            .SaveScoreConfigurationAsync(
                request,
                ct);

        return Ok(true);
    }
}