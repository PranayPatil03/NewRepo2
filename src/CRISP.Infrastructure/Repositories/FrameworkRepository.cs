using System.Text.Json;
using CRISP.Application.DTOs;
using CRISP.Application.Interfaces;
using CRISP.Domain.Entities;
using CRISP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CRISP.Infrastructure.Repositories;
/// <summary>Compatibility adapter for the unchanged V19 UI/API contracts. All persistence uses only the new schema tables.</summary>
public sealed class FrameworkRepository(CrispDbContext db) : IFrameworkRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };
    private IQueryable<WorkflowJsonVersion> Versions(string code) => db.WorkflowJsonVersions.Where(v => v.WorkflowCode == code);
    private async Task<WorkflowJsonVersion?> Latest(string code, CancellationToken ct = default) => await Versions(code).OrderByDescending(v => v.VersionNo).FirstOrDefaultAsync(ct);
    private static WorkflowDto ToWorkflow(WorkflowJsonVersion v) => new(v.WorkflowJsonVersionId, v.WorkflowCode, v.WorkflowName, true);
    private static VersionDto ToVersion(WorkflowJsonVersion v)
    {
        return new VersionDto(
            v.WorkflowJsonVersionId,
            v.VersionNo,
            v.VersionName,
            v.Status,
            v.IsPublished,
            DateTime.UtcNow,
            "Admin")
        {
            JsonContent = v.JsonContent,
            OverallWeightage = 100m
        };
    }
    private async Task<WorkflowDefinitionDto> Definition(WorkflowJsonVersion v, CancellationToken ct)
    {
        try
        {
            var root = JsonDocument.Parse(v.JsonContent).RootElement;
            if (root.ValueKind == JsonValueKind.Array) root = root.EnumerateArray().FirstOrDefault();
            return JsonSerializer.Deserialize<WorkflowDefinitionDto>(root.GetRawText(), JsonOptions) ?? new()
            {
                WorkflowName = v.WorkflowName
            };
        }
        catch
        {
            return new()
            {
                WorkflowName = v.WorkflowName
            };
        }
    }
    private async Task<List<ParameterDto>> Params(WorkflowJsonVersion v, CancellationToken ct, int? industryId = null)
    {
        var def = await Definition(v, ct);
        var maps = await db.IndustryParameterMappings.Where(m => m.WorkflowJsonVersionId == v.WorkflowJsonVersionId && m.IndustryDetailsId == (industryId ?? v.IndustryDetailsId) && m.IsActive).ToListAsync(ct);
        var masters = await db.ParameterDefinitionMasters.Where(p => p.IsActive).ToDictionaryAsync(p => p.ParameterDefinitionMasterId, ct);
        var result = new List<ParameterDto>();
        foreach (var d in def.Parameters)
        {
            var map = maps.FirstOrDefault(m => masters.TryGetValue(m.ParameterDefinitionMasterId, out var p) && p.ParameterCode == d.ParameterCode);
            if (map == null) continue;
            var master = masters[map.ParameterDefinitionMasterId];
            var scales = await db.ScaleConfigurationDetails.Where(s => s.IndustryParameterMappingId == map.IndustryParameterMappingId).OrderBy(s => s.ScoreValue).ToListAsync(ct);
            var scores = scales.Select(s => new ScoreDto((int)s.ScoreValue, s.Definition, null, null, s.MinValue, s.MaxValue, true, true)).ToList();
            if (scores.Count == 0) scores = d.Scores.Select(x => new ScoreDto(x.Score, x.Definition, x.ThresholdValue, x.Unit, x.MinValue, x.MaxValue, x.MinInclusive, x.MaxInclusive)).ToList();
            result.Add(new ParameterDto(map.IndustryParameterMappingId, d.ParameterCode, map.ParameterDisplayName.Length > 0 ? map.ParameterDisplayName : master.ParameterName, master.WhyItMatters, d.HowDoWeMeasure, map.Weight, d.DisplayOrder, map.ValueType.Equals("Value", StringComparison.OrdinalIgnoreCase) || d.IsValueDriven, scores));
        }
        return result;
    }
    public async Task<IReadOnlyList<WorkflowDto>> GetWorkflowsAsync(CancellationToken ct = default)
    {
        var query = db.WorkflowJsonVersions
            .AsNoTracking()
            .Where(v => !db.WorkflowJsonVersions.Any(other =>
                other.WorkflowCode == v.WorkflowCode && other.VersionNo > v.VersionNo))
            .Join(db.IndustryDetails.AsNoTracking(),
                v => v.IndustryDetailsId,
                i => i.IndustryDetailId,
                (v, i) => new { Version = v, Industry = i })
            .OrderBy(x => x.Industry.IndustryName)
            .ThenBy(x => x.Version.WorkflowName)
            .Select(x => new WorkflowDto(
                x.Version.WorkflowJsonVersionId,
                x.Version.WorkflowCode,
                x.Version.WorkflowName,
                true)
            {
                IndustryDetailsId = x.Industry.IndustryDetailId,
                IndustryName = x.Industry.IndustryName
            });

        return await query.ToListAsync(ct);
    }
    public async Task<WorkflowDto> CreateWorkflowAsync(CreateWorkflowRequest request, string skeletonJson, CancellationToken ct = default)
    {
        var code = request.WorkflowCode.Trim();
        if (await db.WorkflowJsonVersions.AnyAsync(v => v.WorkflowCode == code, ct)) throw new InvalidOperationException($"Workflow code '{code}' already exists.");
        if (request.IndustryDetailsId <= 0)
            throw new InvalidOperationException("Select an industry before creating a workflow.");
        var industry = await db.IndustryDetails.FirstOrDefaultAsync(
            x => x.IndustryDetailId == request.IndustryDetailsId && x.IsActive, ct);
        if (industry is null)
            throw new InvalidOperationException("The selected industry does not exist or is inactive.");
        var v = new WorkflowJsonVersion
        {
            IndustryDetailsId = industry.IndustryDetailId,
            WorkflowCode = code,
            WorkflowName = request.WorkflowName.Trim(),
            VersionNo = 1,
            VersionName = $"{DateTime.Today:dd-MMM-yyyy} - Draft",
            Status = "Draft",
            JsonContent = skeletonJson,
            IsPublished = false
        };
        db.WorkflowJsonVersions.Add(v);
        await db.SaveChangesAsync(ct);
        return new WorkflowDto(v.WorkflowJsonVersionId, v.WorkflowCode, v.WorkflowName, true)
        {
            IndustryDetailsId = industry.IndustryDetailId,
            IndustryName = industry.IndustryName
        };
    }
    public async Task<WorkflowContextDto?> GetWorkflowContextAsync(string code, CancellationToken ct = default)
    {
        var v = await Latest(code, ct);
        if (v == null) return null;
        return new()
        {
            Workflow = ToWorkflow(v),
            Steps = (await Definition(v, ct)).Steps.Select((x, i) => new FrameworkStepDto(i + 1, x.StepName, x.DisplayOrder, true)).ToList()
        };
    }
    public async Task<IReadOnlyList<EntityDto>> SearchEntitiesAsync(string? search, CancellationToken ct = default) => await db.CustomerMasters.AsNoTracking().Where(e => e.IsActive && (string.IsNullOrWhiteSpace(search) || e.EntityName.Contains(search))).OrderBy(e => e.EntityName).Select(e => new EntityDto(e.EntityId, e.EntityName)).Take(50).ToListAsync(ct);
    public async Task<IReadOnlyList<FrameworkLookupDto>> GetFrameworkOptionsAsync(int entityId, CancellationToken ct = default)
    {
        var rs = await db.AssessmentResponses.Where(r => r.EntityId == entityId).Include(r => r.WorkflowJsonVersion).ToListAsync(ct);
        return rs.Select(r => new FrameworkLookupDto(r.AssessmentResponseId, $"{r.WorkflowJsonVersion.WorkflowName} - V{r.WorkflowJsonVersion.VersionNo}", r.Status, r.UpdatedOn, r.WorkflowJsonVersion.VersionNo, "")).ToList();
    }
    public async Task<IReadOnlyList<IndustrySegmentDto>>
    GetIndustrySegmentsAsync(
        string code,
        CancellationToken ct = default)
    {
        return await db.WorkflowJsonVersions
            .AsNoTracking()
            .Where(x => x.WorkflowCode == code)
            .Join(
                db.IndustryDetails.AsNoTracking()
                    .Where(x => x.IsActive),
                version => version.IndustryDetailsId,
                industry => industry.IndustryDetailId,
                (version, industry) => new
                {
                    industry.IndustryDetailId,
                    industry.IndustryName
                })
            .Distinct()
            .OrderBy(x => x.IndustryName)
            .Select(x => new IndustrySegmentDto(
                x.IndustryDetailId,
                x.IndustryName))
            .ToListAsync(ct);
    }
    public async Task<IReadOnlyList<FrameworkStepDto>> GetStepsAsync(string code, CancellationToken ct = default)
    {
        var v = await Latest(code, ct);
        return v == null ? [] : (await Definition(v, ct)).Steps.Select((s, i) => new FrameworkStepDto(i + 1, s.StepName, s.DisplayOrder, true)).ToList();
    }
    public async Task<IReadOnlyList<FrameworkListDto>> GetFrameworksAsync(CancellationToken ct = default)
    {
        var rs = await db.AssessmentResponses.Include(r => r.CustomerMaster).Include(r => r.WorkflowJsonVersion).OrderByDescending(r => r.UpdatedOn).ToListAsync(ct);
        return rs.Select(r => new FrameworkListDto(r.AssessmentResponseId, r.CustomerMaster.EntityName, "", r.WorkflowJsonVersion.WorkflowName, r.Status, r.WorkflowJsonVersion.VersionNo, r.UpdatedOn)).ToList();
    }

    public async Task<ParameterDto?> GetParameterAsync(
        int id,
        CancellationToken ct = default)
    {
        var mapping = await db.IndustryParameterMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.IndustryParameterMappingId == id
                     && x.IsActive,
                ct);

        if (mapping is null)
            return null;

        var version = await db.WorkflowJsonVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.WorkflowJsonVersionId == mapping.WorkflowJsonVersionId,
                ct);

        if (version is null)
            return null;

        var master = await db.ParameterDefinitionMasters
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ParameterDefinitionMasterId ==
                     mapping.ParameterDefinitionMasterId,
                ct);

        if (master is null)
            return null;

        var definition = await Definition(version, ct);

        var jsonParameter = definition.Parameters
            .FirstOrDefault(x =>
                string.Equals(
                    x.ParameterCode,
                    master.ParameterCode,
                    StringComparison.OrdinalIgnoreCase));

        var scaleDetails = await db.ScaleConfigurationDetails
            .AsNoTracking()
            .Where(x =>
                x.IndustryParameterMappingId ==
                mapping.IndustryParameterMappingId)
            .OrderBy(x => x.ScoreValue)
            .ToListAsync(ct);

        var scores = scaleDetails
            .Select(x => new ScoreDto(
                (int)x.ScoreValue,
                x.Definition,
                null,
                null,
                x.MinValue,
                x.MaxValue,
                true,
                true))
            .ToList();

        // If no database scale rows exist, use the selected version's JSON.
        if (scores.Count == 0 && jsonParameter is not null)
        {
            scores = jsonParameter.Scores
                .Select(x => new ScoreDto(
                    x.Score,
                    x.Definition,
                    x.ThresholdValue,
                    x.Unit,
                    x.MinValue,
                    x.MaxValue,
                    x.MinInclusive,
                    x.MaxInclusive))
                .ToList();
        }

        return new ParameterDto(
            mapping.IndustryParameterMappingId,
            master.ParameterCode,
            string.IsNullOrWhiteSpace(mapping.ParameterDisplayName)
                ? master.ParameterName
                : mapping.ParameterDisplayName,
            master.WhyItMatters,
            jsonParameter?.HowDoWeMeasure ?? string.Empty,
            mapping.Weight,
            mapping.DisplayOrder,
            string.Equals(
                mapping.ValueType,
                "Value",
                StringComparison.OrdinalIgnoreCase),
            scores);
    }

    public async Task<IReadOnlyList<ParameterDto>> GetParametersAsync(string code, CancellationToken ct = default)
    {
        var v = await Latest(code, ct);
        return v == null ? [] : await Params(v, ct);
    }


    public async Task<IReadOnlyList<ParameterDto>> GetParameterMastersAsync(
    CancellationToken ct = default)
    {
        var masters = await db.ParameterDefinitionMasters
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ParameterName)
            .ToListAsync(ct);

        return masters
            .Select(x => new ParameterDto(x.ParameterDefinitionMasterId, x.ParameterCode, x.ParameterName, x.WhyItMatters, string.Empty, 0m, 0, false, []))
            .ToList();
    }

    public async Task<ParameterDto> SaveParameterMasterAsync(
    ParameterDto request,
    CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ParameterCode))
        {
            throw new InvalidOperationException(
                "Parameter code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ParameterName))
        {
            throw new InvalidOperationException(
                "Parameter name is required.");
        }

        var parameterCode = request.ParameterCode.Trim();
        var parameterName = request.ParameterName.Trim();

        ParameterDefinitionMaster? entity = null;

        // Update existing master
        if (request.Id > 0)
        {
            entity = await db.ParameterDefinitionMasters
                .FirstOrDefaultAsync(
                    x => x.ParameterDefinitionMasterId == request.Id,
                    ct);

            if (entity is null)
            {
                throw new InvalidOperationException(
                    $"Parameter master {request.Id} was not found.");
            }
        }

        // Check duplicate parameter code
        var duplicate = await db.ParameterDefinitionMasters
            .AnyAsync(
                x =>
                    x.ParameterCode == parameterCode &&
                    x.ParameterDefinitionMasterId != request.Id,
                ct);

        if (duplicate)
        {
            throw new InvalidOperationException(
                $"Parameter code '{parameterCode}' already exists.");
        }

        // Create
        if (entity is null)
        {
            entity = new ParameterDefinitionMaster
            {
                ParameterCode = parameterCode,
                ParameterName = parameterName,
                WhyItMatters = request.WhyItMatters ?? string.Empty,
                IsActive = true
            };

            db.ParameterDefinitionMasters.Add(entity);
        }
        else
        {
            // Update
            entity.ParameterCode = parameterCode;
            entity.ParameterName = parameterName;
            entity.WhyItMatters =
                request.WhyItMatters ?? string.Empty;

            entity.IsActive = true;
        }

        await db.SaveChangesAsync(ct);

        return new ParameterDto(
            entity.ParameterDefinitionMasterId,
            entity.ParameterCode,
            entity.ParameterName,
            entity.WhyItMatters,
            string.Empty,
            0m,
            0,
            false,
            []);
    }

    public async Task<IReadOnlyList<VersionDto>> GetVersionsAsync(string code, CancellationToken ct = default) => (await Versions(code).OrderByDescending(v => v.VersionNo).ToListAsync(ct)).Select(ToVersion).ToList();
    public async Task<VersionDto?> GetVersionAsync(
    string code,
    int versionNo,
    CancellationToken ct = default)
    {
        var version = await Versions(code)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.VersionNo == versionNo,
                ct);

        return version is null
            ? null
            : ToVersion(version);
    }

    private async Task<AssessmentResponse?> Response(int id, CancellationToken ct) => await db.AssessmentResponses.Include(r => r.CustomerMaster).Include(r => r.WorkflowJsonVersion).FirstOrDefaultAsync(r => r.AssessmentResponseId == id, ct);
    public async Task<FrameworkDto?> GetFrameworkShellAsync(int id, CancellationToken ct = default)
    {
        var r = await Response(id, ct);
        if (r == null) return null;
        var v = r.WorkflowJsonVersion;
        var ind = await db.IndustryDetails.FindAsync([v.IndustryDetailsId], ct);
        var segments = ReadIndustrySegments(r.ResponseJson);
        if (segments.Count == 0 && ind is not null)
            segments = [new BusinessParameterDetailsDto(ind.IndustryDetailId, ind.IndustryDetailId, ind.IndustryName, 100, r.Status)];
        var p = new List<ParameterDto>();
        foreach (var segment in segments)
            p.AddRange(await Params(v, ct, segment.IndustrySegmentId));
        p = p.GroupBy(x => x.Id).Select(x => x.First()).OrderBy(x => x.DisplayOrder).ToList();
        return new FrameworkDto
        {
            Id = r.AssessmentResponseId,
            EntityId = r.EntityId,
            EntityName = r.CustomerMaster.EntityName,
            WorkflowCode = v.WorkflowCode,
            IndustryName = ind?.IndustryName ?? "",
            FrameworkName = v.WorkflowName,
            Status = r.Status,
            WorkflowVersionNo = v.VersionNo,
            FrameworkDate = r.UpdatedOn,
            Parameters = p,
            Steps = (await Definition(v, ct)).Steps.Select((s, i) => new FrameworkStepDto(i + 1, s.StepName, s.DisplayOrder, true)).ToList(),
            IndustrySegments = segments,
            Workflow = new WorkflowSummaryDto
            {
                Id = v.WorkflowJsonVersionId,
                WorkflowCode = v.WorkflowCode,
                WorkflowName = v.WorkflowName,
                IsActive = true
            }
        };
    }
    public async Task<IReadOnlyList<FrameworkParameterDto>> GetFrameworkParameterValuesAsync(int id, CancellationToken ct = default)
    {
        var r = await Response(id, ct);
        if (r == null) return [];
        return ReadValues(r.ResponseJson);
    }
    private static List<BusinessParameterDetailsDto> ReadIndustrySegments(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("IndustrySegments", out var items) || items.ValueKind != JsonValueKind.Array)
                return [];
            return items.EnumerateArray().Select(x => new BusinessParameterDetailsDto(
                x.TryGetProperty("Id", out var id) ? id.GetInt32() : 0,
                x.TryGetProperty("IndustrySegmentId", out var industryId) ? industryId.GetInt32() : 0,
                x.TryGetProperty("IndustryName", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                x.TryGetProperty("Weight", out var weight) ? weight.GetDecimal() : 0m,
                x.TryGetProperty("Status", out var status) ? status.GetString() ?? string.Empty : string.Empty))
                .Where(x => x.IndustrySegmentId > 0)
                .ToList();
        }
        catch (JsonException) { return []; }
    }

    private static List<FrameworkParameterDto> ReadValues(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            var root = d.RootElement;
            if (root.TryGetProperty("Values", out var values) && values.ValueKind == JsonValueKind.Array) return values.EnumerateArray().Select(x => new FrameworkParameterDto(x.TryGetProperty("ParameterId", out var id) ? id.GetInt32() : 0, x.TryGetProperty("ParameterName", out var n) ? n.GetString() ?? "" : "", x.TryGetProperty("Weight", out var w) ? w.GetDecimal() : 0, x.TryGetProperty("Value", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null, x.TryGetProperty("Score", out var s) ? s.GetInt32() : 0)).ToList();
        }
        catch
        {
        }
        return [];
    }
    public async Task<string?> GetFrameworkValueJsonAsync(int id, CancellationToken ct = default) => (await Response(id, ct))?.ResponseJson;
    public async Task<string?> GetFrameworkValueJsonAsync(int id, int versionNo, CancellationToken ct = default)
    {
        var baseResponse = await Response(id, ct);
        if (baseResponse == null) return null;
        var target = await Versions(baseResponse.WorkflowJsonVersion.WorkflowCode).FirstOrDefaultAsync(v => v.VersionNo == versionNo, ct);
        if (target == null) return null;
        return await db.AssessmentResponses.Where(r => r.EntityId == baseResponse.EntityId && r.WorkflowJsonVersionId == target.WorkflowJsonVersionId).Select(r => r.ResponseJson).FirstOrDefaultAsync(ct);
    }
    public async Task SaveFrameworkValueJsonAsync(int id, int versionNo, string json, CancellationToken ct = default)
    {
        var baseResponse = await Response(id, ct) ?? throw new InvalidOperationException("Assessment response not found.");
        var target = await Versions(baseResponse.WorkflowJsonVersion.WorkflowCode).FirstOrDefaultAsync(v => v.VersionNo == versionNo, ct) ?? throw new InvalidOperationException("Workflow version not found.");
        var r = await db.AssessmentResponses.FirstOrDefaultAsync(x => x.EntityId == baseResponse.EntityId && x.WorkflowJsonVersionId == target.WorkflowJsonVersionId, ct);
        if (r == null)
        {
            r = new AssessmentResponse
            {
                EntityId = baseResponse.EntityId,
                WorkflowJsonVersionId = target.WorkflowJsonVersionId,
                ResponseJson = json,
                Status = "Draft",
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            };
            db.AssessmentResponses.Add(r);
        }
        else
        {
            r.ResponseJson = json;
            r.UpdatedOn = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
    public async Task ClearFrameworkValueJsonAsync(int id, CancellationToken ct = default)
    {
        var r = await Response(id, ct);
        if (r != null)
        {
            r.ResponseJson = "{}";
            r.UpdatedOn = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
    public async Task<FrameworkDto?> GetFrameworkAsync(int id, int? versionNo = null, CancellationToken ct = default)
    {
        var shell = await GetFrameworkShellAsync(id, ct);
        if (shell == null) return null;
        var baseResponse = await Response(id, ct);
        if (baseResponse == null) return shell;
        var version = versionNo.HasValue
     ? await Versions(shell.WorkflowCode)
         .FirstOrDefaultAsync(v => v.VersionNo == versionNo.Value, ct)
     : baseResponse.WorkflowJsonVersion;
        if (version != null)
        {
            shell.WorkflowVersionNo = version.VersionNo;
            var versionParameters = new List<ParameterDto>();
            foreach (var segment in shell.IndustrySegments)
                versionParameters.AddRange(await Params(version, ct, segment.IndustrySegmentId));
            shell.Parameters = versionParameters.GroupBy(x => x.Id).Select(x => x.First()).OrderBy(x => x.DisplayOrder).ToList();
            var response = await db.AssessmentResponses.FirstOrDefaultAsync(r => r.EntityId == baseResponse.EntityId && r.WorkflowJsonVersionId == version.WorkflowJsonVersionId, ct);
            shell.Id = response?.AssessmentResponseId ?? 0;
            shell.ParameterValues = response != null ? ReadValues(response.ResponseJson) : shell.Parameters.Select(p => new FrameworkParameterDto(p.Id, p.ParameterName, p.Weight, null, 0)).ToList();
        }
        return shell;
    }
    public async Task<FrameworkDto> SaveFrameworkAsync(FrameworkDto f, CancellationToken ct = default)
    {
        var v = await Versions(f.WorkflowCode).FirstOrDefaultAsync(x => x.VersionNo == f.WorkflowVersionNo, ct) ?? await Latest(f.WorkflowCode, ct) ?? throw new InvalidOperationException("Workflow version not found.");
        var entity = await db.CustomerMasters.FirstOrDefaultAsync(e => e.EntityId == f.EntityId, ct);
        if (entity == null)
        {
            entity = new CustomerMaster
            {
                EntityName = f.EntityName.Trim(),
                IsActive = true
            };
            db.CustomerMasters.Add(entity);
            await db.SaveChangesAsync(ct);
        }
        var r = f.Id > 0 ? await db.AssessmentResponses.FirstOrDefaultAsync(x => x.AssessmentResponseId == f.Id, ct) : null;
        if (r == null) r = await db.AssessmentResponses.FirstOrDefaultAsync(x => x.EntityId == entity.EntityId && x.WorkflowJsonVersionId == v.WorkflowJsonVersionId, ct);
        if (r == null)
        {
            r = new AssessmentResponse
            {
                EntityId = entity.EntityId,
                WorkflowJsonVersionId = v.WorkflowJsonVersionId,
                Status = f.Status,
                ResponseJson = "{}"
            };
            db.AssessmentResponses.Add(r);
        }
        r.EntityId = entity.EntityId;
        r.WorkflowJsonVersionId = v.WorkflowJsonVersionId;
        r.Status = f.Status;
        r.UpdatedOn = DateTime.UtcNow;
        r.ResponseJson = JsonSerializer.Serialize(new
        {
            FrameworkId = r.AssessmentResponseId,
            WorkflowCode = f.WorkflowCode,
            WorkflowVersionNo = v.VersionNo,
            EntityId = entity.EntityId,
            EntityName = entity.EntityName,
            IndustrySegments = f.IndustrySegments,
            Values = f.ParameterValues.Select(x => new {
                ParameterId = x.ParameterId,
                ParameterCode = f.Parameters.FirstOrDefault(p => p.Id == x.ParameterId)?.ParameterCode ?? "",
                ParameterName = x.ParameterName,
                Weight = x.Weight,
                Value = x.Value,
                Score = x.Score
            })
        });
        await db.SaveChangesAsync(ct);
        return await GetFrameworkShellAsync(r.AssessmentResponseId, ct) ?? f;
    }
    public async Task<VersionDto> SaveVersionAsync(SaveVersionRequest req, CancellationToken ct = default)
    {
        var latest = await Latest(req.WorkflowCode, ct) ?? throw new InvalidOperationException("Workflow not found.");
        var no = req.VersionNo > 0 ? req.VersionNo : ((await Versions(req.WorkflowCode).MaxAsync(v => (int?)v.VersionNo, ct) ?? 0) + 1);
        var v = new WorkflowJsonVersion
        {
            IndustryDetailsId = latest.IndustryDetailsId,
            WorkflowCode = req.WorkflowCode,
            WorkflowName = latest.WorkflowName,
            VersionNo = no,
            VersionName = req.VersionName,
            Status = req.Status,
            JsonContent = req.JsonContent,
            IsPublished = req.IsPublished
        };
        db.WorkflowJsonVersions.Add(v);
        await db.SaveChangesAsync(ct);
        return ToVersion(v);
    }
    public async Task<WorkflowDefinitionDto?> GetWorkflowDefinitionAsync(
    string code,
    CancellationToken ct = default)
    {
        var version = await Latest(code, ct);

        if (version == null)
            return null;

        var definition = await Definition(version, ct);

        definition.WorkflowName = version.WorkflowName;

        var dbRules = await db.RuleDefinitions
            .AsNoTracking()
            .Where(x =>
                x.WorkflowJsonVersionId == version.WorkflowJsonVersionId &&
                x.IsActive)
            .OrderBy(x => x.RuleDefinitionId)
            .ToListAsync(ct);

        foreach (var definitionRule in definition.Rules)
        {
            var databaseRule = dbRules.FirstOrDefault(x =>
                x.RuleName == definitionRule.RuleName);

            if (databaseRule == null)
                continue;

            definitionRule.RuleExpressionType =
                databaseRule.RuleExpressionType;

            definitionRule.OutputExpression =
                databaseRule.OutputExpression;
        }

        return definition;
    }

    public async Task<VersionDto> SaveWorkflowDefinitionAsync(
    SaveWorkflowDefinitionRequest req,
    string json,
    CancellationToken ct = default)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(ct);

        try
        {
            var latest =
                await Latest(req.WorkflowCode, ct)
                ?? throw new InvalidOperationException(
                    "Workflow not found.");

            // Do not blindly clone from the highest version number: a newer Draft may
            // be empty. Choose the newest version that actually has parameters or active
            // industry mappings, while keeping the workflow's industry unchanged.
            var candidateVersions = await Versions(req.WorkflowCode)
                .OrderByDescending(x => x.VersionNo)
                .ToListAsync(ct);

            WorkflowJsonVersion sourceVersion = latest;
            WorkflowDefinitionDto? sourceDefinition = null;
            foreach (var candidate in candidateVersions)
            {
                var candidateDefinition = await Definition(candidate, ct);
                var hasParameters = candidateDefinition.Parameters.Count > 0;
                var hasMappings = await db.IndustryParameterMappings.AnyAsync(
                    x => x.WorkflowJsonVersionId == candidate.WorkflowJsonVersionId &&
                         x.IndustryDetailsId == candidate.IndustryDetailsId &&
                         x.IsActive,
                    ct);

                if (hasParameters || hasMappings)
                {
                    sourceVersion = candidate;
                    sourceDefinition = candidateDefinition;
                    break;
                }
            }

            sourceDefinition ??= await Definition(sourceVersion, ct);

            // If the submitted definition is blank, copy the newest configured definition
            // rather than copying an empty later Draft. Never modify that source version.
            if (req.Definition.Parameters.Count == 0 &&
                req.Definition.Rules.Count == 0 &&
                req.Definition.Validations.Count == 0)
            {
                req.Definition = sourceDefinition;
                json = JsonSerializer.Serialize(new[] { req.Definition }, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = null
                });
            }

            var no =
                (await Versions(req.WorkflowCode)
                    .MaxAsync(
                        v => (int?)v.VersionNo,
                        ct) ?? 0) + 1;

            // =====================================================
            // ARCHIVE PREVIOUS PUBLISHED VERSIONS
            // =====================================================

            if (req.IsPublished ||
                string.Equals(
                    req.Status,
                    "Published",
                    StringComparison.OrdinalIgnoreCase))
            {
                var previousPublished =
                    await db.WorkflowJsonVersions
                        .Where(x =>
                            x.WorkflowCode == req.WorkflowCode &&
                            (x.IsPublished ||
                             x.Status == "Published"))
                        .ToListAsync(ct);

                foreach (var oldVersion in previousPublished)
                {
                    oldVersion.Status = "Archieve";
                    oldVersion.IsPublished = false;
                }
            }

            // =====================================================
            // CREATE NEW VERSION
            // =====================================================

            var v = new WorkflowJsonVersion
            {
                IndustryDetailsId = latest.IndustryDetailsId,

                WorkflowCode = latest.WorkflowCode,

                WorkflowName =
                    string.IsNullOrWhiteSpace(
                        req.Definition.WorkflowName)
                        ? latest.WorkflowName
                        : req.Definition.WorkflowName,

                VersionNo = no,

                VersionName = req.VersionName,

                Status = req.IsPublished
                    ? "Published"
                    : "Draft",

                JsonContent = json,

                IsPublished = req.IsPublished
            };

            db.WorkflowJsonVersions.Add(v);

            // This SaveChanges archives old published versions
            // AND inserts the new version together.
            await db.SaveChangesAsync(ct);

            // Clone version-specific mapping and score rows first. ScaleConfiguration is a
            // reusable master, while ScaleConfigurationDetails are copied per mapping/version.
            var previousMappings = await db.IndustryParameterMappings
                .Where(x =>
                    x.WorkflowJsonVersionId == sourceVersion.WorkflowJsonVersionId &&
                    x.IndustryDetailsId == v.IndustryDetailsId &&
                    x.IsActive)
                .ToListAsync(ct);
            var clonedMappingByMaster = new Dictionary<int, IndustryParameterMapping>();
            foreach (var previousMapping in previousMappings)
            {
                var clonedMapping = new IndustryParameterMapping
                {
                    WorkflowJsonVersionId = v.WorkflowJsonVersionId,
                    IndustryDetailsId = previousMapping.IndustryDetailsId,
                    ParameterDefinitionMasterId = previousMapping.ParameterDefinitionMasterId,
                    ParentIndustryParameterMappingId = null,
                    ParameterDisplayName = previousMapping.ParameterDisplayName,
                    ValueType = previousMapping.ValueType,
                    Weight = previousMapping.Weight,
                    DisplayOrder = previousMapping.DisplayOrder,
                    IsActive = true
                };
                db.IndustryParameterMappings.Add(clonedMapping);
                clonedMappingByMaster[previousMapping.ParameterDefinitionMasterId] = clonedMapping;
            }
            if (clonedMappingByMaster.Count > 0)
            {
                await db.SaveChangesAsync(ct);
                var previousIds = previousMappings.Select(x => x.IndustryParameterMappingId).ToList();
                var previousScaleDetails = await db.ScaleConfigurationDetails
                    .Where(x => previousIds.Contains(x.IndustryParameterMappingId)).ToListAsync(ct);
                var previousIdToMaster = previousMappings.ToDictionary(x => x.IndustryParameterMappingId, x => x.ParameterDefinitionMasterId);
                foreach (var detail in previousScaleDetails)
                {
                    if (!previousIdToMaster.TryGetValue(detail.IndustryParameterMappingId, out var masterId) ||
                        !clonedMappingByMaster.TryGetValue(masterId, out var clonedMapping)) continue;
                    db.ScaleConfigurationDetails.Add(new ScaleConfigurationDetails
                    {
                        IndustryParameterMappingId = clonedMapping.IndustryParameterMappingId,
                        ScaleConfigurationId = detail.ScaleConfigurationId,
                        ScoreValue = detail.ScoreValue,
                        Definition = detail.Definition,
                        MinValue = detail.MinValue,
                        MaxValue = detail.MaxValue
                    });
                }
                await db.SaveChangesAsync(ct);
            }

            // =====================================================
            // PARAMETERS
            // =====================================================

            foreach (var p in req.Definition.Parameters)
            {
                var parameterCode = p.ParameterCode?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(parameterCode))
                    continue;

                var master =
                    await db.ParameterDefinitionMasters
                        .FirstOrDefaultAsync(
                            x => x.ParameterCode.ToLower() == parameterCode.ToLower(),
                            ct);

                if (master == null)
                {
                    master =
                        new ParameterDefinitionMaster
                        {
                            ParameterCode = parameterCode,
                            ParameterName = p.ParameterName,
                            WhyItMatters = p.WhyItMatters,
                            IsActive = true
                        };

                    db.ParameterDefinitionMasters.Add(master);

                    await db.SaveChangesAsync(ct);
                }

                var map = await db.IndustryParameterMappings.FirstOrDefaultAsync(x =>
    x.WorkflowJsonVersionId == v.WorkflowJsonVersionId &&
    x.IndustryDetailsId == v.IndustryDetailsId &&
    x.ParameterDefinitionMasterId == master.ParameterDefinitionMasterId,
    ct);
                if (map is null)
                {
                    map = new IndustryParameterMapping
                    {
                        WorkflowJsonVersionId = v.WorkflowJsonVersionId,
                        IndustryDetailsId = v.IndustryDetailsId,
                        ParameterDefinitionMasterId = master.ParameterDefinitionMasterId,
                        ParameterDisplayName = p.ParameterName,
                        ValueType = p.IsValueDriven ? "Value" : "ManualScore",
                        Weight = p.Weight,
                        DisplayOrder = p.DisplayOrder > 0 ? p.DisplayOrder : 9999,
                        IsActive = true
                    };
                    db.IndustryParameterMappings.Add(map);
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    map.ParameterDisplayName = p.ParameterName;
                    map.ValueType = p.IsValueDriven ? "Value" : "ManualScore";
                    map.Weight = p.Weight;
                    map.IsActive = true;
                }

                // =================================================
                // SCALE
                // =================================================

                var existingScaleDetails = await db.ScaleConfigurationDetails
                    .AnyAsync(x => x.IndustryParameterMappingId == map.IndustryParameterMappingId, ct);
                if (p.Scores.Count > 0 && !existingScaleDetails)
                {
                    var scaleCode = $"{p.ParameterCode}_SCALE";
                    var sc = await db.ScaleConfigurations.FirstOrDefaultAsync(x => x.ScaleCode == scaleCode, ct);
                    if (sc is null)
                    {
                        sc = new ScaleConfiguration
                        {
                            ScaleCode = scaleCode,
                            ScaleName = $"{p.ParameterName} scale",
                            MinValue = p.Scores.Min(x => x.MinValue ?? 0),
                            MaxValue = p.Scores.Max(x => x.MaxValue ?? 5),
                            IsActive = true
                        };
                        db.ScaleConfigurations.Add(sc);
                        await db.SaveChangesAsync(ct);
                    }

                    foreach (var s in p.Scores)
                    {
                        db.ScaleConfigurationDetails.Add(
                            new ScaleConfigurationDetails
                            {
                                IndustryParameterMappingId =
                                    map.IndustryParameterMappingId,

                                ScaleConfigurationId =
                                    sc.ScaleConfigurationId,

                                ScoreValue = s.Score,

                                Definition = s.Definition,

                                MinValue =
                                    s.MinValue ?? sc.MinValue,

                                MaxValue =
                                    s.MaxValue ?? sc.MaxValue
                            });
                    }
                }
            }

            // =====================================================
            // RULES
            // =====================================================

            foreach (var r in req.Definition.Rules)
            {
                db.RuleDefinitions.Add(
                    new RuleDefinition
                    {
                        WorkflowJsonVersionId =
                            v.WorkflowJsonVersionId,

                        RuleName = r.RuleName,

                        RuleExpressionType =
                            r.RuleExpressionType,

                        OutputExpression =
                            r.OutputExpression,

                        IsActive = true
                    });
            }

            await db.SaveChangesAsync(ct);

            // =====================================================
            // VALIDATIONS
            // =====================================================

            var maps =
                await db.IndustryParameterMappings
                    .Where(x =>
                        x.WorkflowJsonVersionId ==
                        v.WorkflowJsonVersionId &&
                        x.IndustryDetailsId == v.IndustryDetailsId)
                    .ToListAsync(ct);

            foreach (var val in req.Definition.Validations)
            {
                var map = maps.FirstOrDefault();

                if (map != null)
                {
                    db.ValidationDefinitions.Add(
                        new ValidationDefinition
                        {
                            IndustryParameterMappingId =
                                map.IndustryParameterMappingId,

                            ValidationName =
                                val.ValidationName,

                            Expression =
                                val.Expression,

                            ErrorMessage =
                                val.ErrorMessage,

                            IsActive = true
                        });
                }
            }

            await db.SaveChangesAsync(ct);

            // Persist the final active mapping/score/rule state back into this Draft's JSON.
            // This also guarantees Parameters is populated when mappings were cloned from
            // an earlier configured version.
            await SyncDraftJsonFromMappingsAsync(
                v.WorkflowJsonVersionId,
                v.IndustryDetailsId,
                ct);

            // Everything succeeded
            await transaction.CommitAsync(ct);

            return ToVersion(v);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<WorkflowJsonVersion?> GetWorkflowVersionAsync(
    string workflowCode,
    int versionNo,
    CancellationToken ct = default)
    {
        return await db.WorkflowJsonVersions
            .FirstOrDefaultAsync(
                x =>
                    x.WorkflowCode == workflowCode &&
                    x.VersionNo == versionNo,
                ct);
    }


    public async Task<IReadOnlyList<IndustryParameterMappingDto>>
    GetIndustryParameterMappingsAsync(
        string workflowCode,
        int versionNo,
        int industryId,
        CancellationToken ct = default)
    {
        var version = await db.WorkflowJsonVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.WorkflowCode == workflowCode &&
                    x.VersionNo == versionNo,
                ct);

        if (version is null)
        {
            return [];
        }

        var masters = await db.ParameterDefinitionMasters
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ParameterName)
            .ToListAsync(ct);

        var mappings = await db.IndustryParameterMappings
            .AsNoTracking()
            .Where(x =>
                x.WorkflowJsonVersionId == version.WorkflowJsonVersionId &&
                x.IndustryDetailsId == industryId)

            .ToListAsync(ct);

        var activeMappingIds = mappings
            .Where(x => x.IsActive)
            .Select(x => x.IndustryParameterMappingId)
            .ToList();

        var scales = await db.ScaleConfigurationDetails
            .AsNoTracking()
            .Where(x =>
                activeMappingIds.Contains(
                    x.IndustryParameterMappingId))
            .GroupBy(x => x.IndustryParameterMappingId)
            .Select(g => new
            {
                MappingId = g.Key,

                ScaleId = g
                    .Select(x => (int?)x.ScaleConfigurationId)
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(
                x => x.MappingId,
                x => x.ScaleId,
                ct);

        var definition = await Definition(version, ct);
        var jsonOrder = definition.Parameters
            .Select((parameter, index) => new { parameter.ParameterCode, Order = parameter.DisplayOrder > 0 ? parameter.DisplayOrder : index + 1 })
            .GroupBy(x => x.ParameterCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Order, StringComparer.OrdinalIgnoreCase);

        return masters
            .Select(master =>
            {
                var mapping = mappings.FirstOrDefault(x =>
                    x.ParameterDefinitionMasterId ==
                    master.ParameterDefinitionMasterId);

                return new IndustryParameterMappingDto
                {
                    IndustryParameterMappingId =
                        mapping?.IndustryParameterMappingId ?? 0,

                    ParameterDefinitionMasterId =
                        master.ParameterDefinitionMasterId,

                    ParameterCode =
                        master.ParameterCode,

                    MasterParameterName =
                        master.ParameterName,

                    IsMapped =
                        mapping?.IsActive == true,

                    ParameterDisplayName =
                        string.IsNullOrWhiteSpace(
                            mapping?.ParameterDisplayName)
                            ? master.ParameterName
                            : mapping.ParameterDisplayName,

                    ValueType =
                        mapping?.ValueType ?? "ManualScore",

                    Weight =
                        mapping?.Weight ?? 0m,

                    ScaleConfigurationId =
                        mapping is not null &&
                        scales.TryGetValue(mapping.IndustryParameterMappingId, out var scaleId)
                            ? scaleId : null,
                    DisplayOrder = mapping?.DisplayOrder > 0 ? mapping.DisplayOrder : (jsonOrder.TryGetValue(master.ParameterCode, out var order) ? order : 9999)
                };
            })
            .ToList();
    }


    private async Task SyncDraftJsonFromMappingsAsync(
        int workflowJsonVersionId,
        int industryId,
        CancellationToken ct)
    {
        var version = await db.WorkflowJsonVersions
            .FirstOrDefaultAsync(x => x.WorkflowJsonVersionId == workflowJsonVersionId, ct)
            ?? throw new InvalidOperationException("Workflow version not found.");

        if (version.IndustryDetailsId != industryId)
            throw new InvalidOperationException("The selected industry does not match this workflow version.");

        if (version.IsPublished ||
            !version.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Version V{version.VersionNo} is read-only. Create a Draft before changing its configuration.");

        var definition = await Definition(version, ct);
        var mappings = await db.IndustryParameterMappings
            .Where(x => x.WorkflowJsonVersionId == workflowJsonVersionId &&
                        x.IndustryDetailsId == industryId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(ct);

        var masterIds = mappings.Select(x => x.ParameterDefinitionMasterId).Distinct().ToList();
        var masters = await db.ParameterDefinitionMasters
            .Where(x => masterIds.Contains(x.ParameterDefinitionMasterId) && x.IsActive)
            .ToDictionaryAsync(x => x.ParameterDefinitionMasterId, ct);

        var existingByCode = definition.Parameters
            .Where(x => !string.IsNullOrWhiteSpace(x.ParameterCode))
            .GroupBy(x => x.ParameterCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var syncedParameters = new List<WorkflowJsonParameterDto>();
        foreach (var mapping in mappings)
        {
            if (!masters.TryGetValue(mapping.ParameterDefinitionMasterId, out var master))
                continue;

            existingByCode.TryGetValue(master.ParameterCode, out var parameter);
            parameter ??= new WorkflowJsonParameterDto
            {
                ParameterCode = master.ParameterCode,
                ParameterName = master.ParameterName,
                WhyItMatters = master.WhyItMatters,
                Scores = []
            };

            parameter.ParameterCode = master.ParameterCode;
            parameter.ParameterName = string.IsNullOrWhiteSpace(mapping.ParameterDisplayName)
                ? master.ParameterName : mapping.ParameterDisplayName;
            parameter.WhyItMatters = master.WhyItMatters;
            parameter.Weight = mapping.Weight;
            parameter.DisplayOrder = mapping.DisplayOrder > 0 ? mapping.DisplayOrder : 9999;
            parameter.IsValueDriven = mapping.ValueType.Equals("Value", StringComparison.OrdinalIgnoreCase);

            var scaleDetails = await db.ScaleConfigurationDetails
                .Where(x => x.IndustryParameterMappingId == mapping.IndustryParameterMappingId)
                .OrderBy(x => x.ScoreValue)
                .ToListAsync(ct);
            if (scaleDetails.Count > 0)
            {
                parameter.Scores = scaleDetails.Select(x => new WorkflowJsonScoreDto
                {
                    Score = (int)x.ScoreValue,
                    Definition = x.Definition,
                    MinValue = x.MinValue,
                    MaxValue = x.MaxValue,
                    MinInclusive = true,
                    MaxInclusive = true
                }).ToList();
            }
            syncedParameters.Add(parameter);
        }
        definition.Parameters = syncedParameters;
        // Do not leave Weights null/empty when mappings already exist. Build the
        // dictionary from this version's active industry mappings, not global masters.
        definition.Weights = syncedParameters
            .Where(x => !string.IsNullOrWhiteSpace(x.ParameterCode))
            .GroupBy(x => x.ParameterCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Weight, StringComparer.OrdinalIgnoreCase);

        var rules = await db.RuleDefinitions
            .Where(x => x.WorkflowJsonVersionId == workflowJsonVersionId && x.IsActive)
            .OrderBy(x => x.RuleDefinitionId)
            .ToListAsync(ct);
        foreach (var rule in rules)
        {
            var existingRule = definition.Rules.FirstOrDefault(x =>
                x.RuleName.Equals(rule.RuleName, StringComparison.OrdinalIgnoreCase));
            if (existingRule is null)
            {
                definition.Rules.Add(new WorkflowJsonRuleDto
                {
                    RuleName = rule.RuleName,
                    RuleExpressionType = rule.RuleExpressionType,
                    OutputExpression = rule.OutputExpression
                });
            }
            else
            {
                existingRule.RuleExpressionType = rule.RuleExpressionType;
                existingRule.OutputExpression = rule.OutputExpression;
            }
        }

        var mappingIds = mappings.Select(x => x.IndustryParameterMappingId).ToList();
        var validations = await db.ValidationDefinitions
            .Where(x => mappingIds.Contains(x.IndustryParameterMappingId) && x.IsActive)
            .OrderBy(x => x.ValidationDefinitionId)
            .ToListAsync(ct);
        foreach (var validation in validations)
        {
            if (!definition.Validations.Any(x => x.ValidationName.Equals(
                    validation.ValidationName, StringComparison.OrdinalIgnoreCase)))
            {
                definition.Validations.Add(new WorkflowJsonValidationDto
                {
                    ValidationName = validation.ValidationName,
                    Expression = validation.Expression,
                    ErrorMessage = validation.ErrorMessage
                });
            }
        }

        version.JsonContent = JsonSerializer.Serialize(new[] { definition }, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveIndustryParameterMappingsAsync(
    int workflowJsonVersionId,
    int industryId,
    IReadOnlyList<IndustryParameterMappingDto> mappings,
    CancellationToken ct = default)
    {
        var targetVersion = await db.WorkflowJsonVersions
            .FirstOrDefaultAsync(x => x.WorkflowJsonVersionId == workflowJsonVersionId, ct)
            ?? throw new InvalidOperationException("Workflow version not found.");

        if (targetVersion.IndustryDetailsId != industryId)
            throw new InvalidOperationException("The selected industry does not match this workflow version.");

        if (targetVersion.IsPublished ||
            !targetVersion.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Version V{targetVersion.VersionNo} is read-only. Create a Draft before changing its configuration.");

        var existingMappings =
            await db.IndustryParameterMappings
                .Where(x =>
                    x.WorkflowJsonVersionId == workflowJsonVersionId &&
                    x.IndustryDetailsId == industryId)
                .ToListAsync(ct);

        foreach (var item in mappings)
        {
            var existing =
                existingMappings.FirstOrDefault(x =>
                    x.ParameterDefinitionMasterId ==
                    item.ParameterDefinitionMasterId);

            // Parameter was unchecked
            if (!item.IsMapped)
            {
                if (existing is not null)
                {
                    existing.IsActive = false;
                }

                continue;
            }

            var displayName =
                string.IsNullOrWhiteSpace(
                    item.ParameterDisplayName)
                    ? item.MasterParameterName
                    : item.ParameterDisplayName.Trim();

            var valueType =
                string.IsNullOrWhiteSpace(item.ValueType)
                    ? "ManualScore"
                    : item.ValueType.Trim();

            // New mapping
            if (existing is null)
            {
                existing = new IndustryParameterMapping
                {
                    WorkflowJsonVersionId =
                        workflowJsonVersionId,
                    IndustryDetailsId = industryId,

                    ParameterDefinitionMasterId =
                        item.ParameterDefinitionMasterId,

                    ParameterDisplayName =
                        displayName,

                    ValueType =
                        valueType,

                    Weight =
                        item.Weight,
                    DisplayOrder = item.DisplayOrder > 0 ? item.DisplayOrder : 9999,

                    IsActive =
                        true
                };

                db.IndustryParameterMappings.Add(existing);
            }
            else
            {
                // Existing mapping
                existing.ParameterDisplayName =
                    displayName;

                existing.ValueType =
                    valueType;

                existing.Weight =
                    item.Weight;
                existing.DisplayOrder = item.DisplayOrder > 0 ? item.DisplayOrder : 9999;

                existing.IsActive =
                    true;
            }
        }

        await db.SaveChangesAsync(ct);
        await SyncDraftJsonFromMappingsAsync(workflowJsonVersionId, industryId, ct);
    }

    public async Task<WorkflowDefinitionDto?> BuildWorkflowDefinitionAsync(
    string workflowCode,
    int versionNo,
    CancellationToken ct = default)
    {
        var version = await db.WorkflowJsonVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.WorkflowCode == workflowCode &&
                     x.VersionNo == versionNo,
                ct);

        if (version is null)
            return null;

        // Keep JSON-only information such as GlobalParams / Steps.
        var definition = await Definition(version, ct);

        definition.WorkflowName = version.WorkflowName;

        // Keep the JSON definition shared by this workflow version. Industry-specific
        // enabled parameters, weights, order and scales are retrieved from
        // IndustryParameterMapping using the selected industry ID.
        return definition;
    }

    public async Task<WorkflowJsonVersion?> PublishVersionAsync(
    string workflowCode,
    int versionNo,
    string jsonContent,
    CancellationToken ct = default)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(ct);

        try
        {
            // Get selected version
            var version = await db.WorkflowJsonVersions
                .FirstOrDefaultAsync(
                    x =>
                        x.WorkflowCode == workflowCode &&
                        x.VersionNo == versionNo,
                    ct);

            if (version is null)
            {
                await transaction.RollbackAsync(ct);
                return null;
            }

            // Archive ALL other versions which are currently Published.
            // Check both fields because your old data may have
            // Status = Published and/or IsPublished = true.
            var oldPublishedVersions =
                await db.WorkflowJsonVersions
                    .Where(x =>
                        x.WorkflowCode == workflowCode &&
                        x.WorkflowJsonVersionId !=
                            version.WorkflowJsonVersionId &&
                        (
                            x.IsPublished ||
                            x.Status == "Published"
                        ))
                    .ToListAsync(ct);

            foreach (var oldVersion in oldPublishedVersions)
            {
                oldVersion.Status = "Archieve";
                oldVersion.IsPublished = false;
            }

            // Publish selected version
            version.JsonContent = jsonContent;
            version.Status = "Published";
            version.IsPublished = true;

            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            return version;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<IndustrySegmentDto>>
    GetAllIndustriesAsync(
        CancellationToken ct = default)
    {
        return await db.IndustryDetails
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.IndustryName)
            .Select(x => new IndustrySegmentDto(
                x.IndustryDetailId,
                x.IndustryName))
            .ToListAsync(ct);
    }

    public async Task<ScoreConfigurationDto?> GetScoreConfigurationAsync(
    int industryParameterMappingId,
    CancellationToken ct = default)
    {
        var mapping =
            await db.IndustryParameterMappings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.IndustryParameterMappingId ==
                        industryParameterMappingId,
                    ct);

        if (mapping is null)
            return null;

        var details =
            await db.ScaleConfigurationDetails
                .AsNoTracking()
                .Where(x =>
                    x.IndustryParameterMappingId ==
                    industryParameterMappingId)
                .OrderBy(x => x.ScoreValue)
                .ToListAsync(ct);

        var scaleId =
            details
                .Select(x => (int?)x.ScaleConfigurationId)
                .FirstOrDefault();

        ScaleConfiguration? scale = null;

        if (scaleId.HasValue)
        {
            scale =
                await db.ScaleConfigurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.ScaleConfigurationId ==
                            scaleId.Value,
                        ct);
        }

        return new ScoreConfigurationDto
        {
            IndustryParameterMappingId =
                industryParameterMappingId,

            ScaleConfigurationId =
                scale?.ScaleConfigurationId,

            ScaleCode =
                scale?.ScaleCode ?? string.Empty,

            ScaleName =
                scale?.ScaleName ?? string.Empty,

            MinValue =
                scale?.MinValue ?? 0,

            MaxValue =
                scale?.MaxValue ?? 0,

            Scores = details
                .Select(x => new ScoreDto
                {
                    Score = (int)x.ScoreValue,

                    Definition =
                        x.Definition,

                    MinValue =
                        x.MinValue,

                    MaxValue =
                        x.MaxValue,

                    MinInclusive = true,

                    MaxInclusive = true
                })
                .ToList()
        };
    }

    public async Task SaveScoreConfigurationAsync(
    ScoreConfigurationDto request,
    CancellationToken ct = default)
    {
        var mapping =
            await db.IndustryParameterMappings
                .FirstOrDefaultAsync(
                    x =>
                        x.IndustryParameterMappingId ==
                        request.IndustryParameterMappingId,
                    ct);

        if (mapping is null)
        {
            throw new InvalidOperationException(
                "Industry parameter mapping was not found.");
        }

        var version =
            await db.WorkflowJsonVersions
                .FirstOrDefaultAsync(
                    x =>
                        x.WorkflowJsonVersionId ==
                        mapping.WorkflowJsonVersionId,
                    ct);

        if (version is null)
        {
            throw new InvalidOperationException(
                "Workflow version was not found.");
        }

        // Published / Archived versions are immutable
        if (version.IsPublished ||
            version.Status.Equals(
                "Published",
                StringComparison.OrdinalIgnoreCase) ||
            version.Status.Equals(
                "Archived",
                StringComparison.OrdinalIgnoreCase) ||
            version.Status.Equals(
                "Archived",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Version V{version.VersionNo} is read-only.");
        }

        if (request.Scores is null ||
            request.Scores.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one score must be configured.");
        }

        if (request.Scores.Any(x =>
                !x.MinValue.HasValue ||
                !x.MaxValue.HasValue))
        {
            throw new InvalidOperationException(
                "Minimum and maximum values are required.");
        }

        if (request.Scores.Any(x =>
                x.MinValue!.Value >
                x.MaxValue!.Value))
        {
            throw new InvalidOperationException(
                "Minimum value cannot be greater than maximum value.");
        }

        if (request.Scores
            .GroupBy(x => x.Score)
            .Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException(
                "Duplicate score values are not allowed.");
        }

        // ---------------------------------------
        // Find/create ScaleConfiguration
        // ---------------------------------------

        ScaleConfiguration? scale = null;

        if (request.ScaleConfigurationId.HasValue)
        {
            scale =
                await db.ScaleConfigurations
                    .FirstOrDefaultAsync(
                        x =>
                            x.ScaleConfigurationId ==
                            request.ScaleConfigurationId.Value,
                        ct);
        }

        if (scale is null)
        {
            var scaleCode =
                request.ScaleCode.Trim();

            if (string.IsNullOrWhiteSpace(scaleCode))
            {
                throw new InvalidOperationException(
                    "Scale code is required.");
            }

            // ScaleCode has a UNIQUE constraint in your DB.
            scale =
                await db.ScaleConfigurations
                    .FirstOrDefaultAsync(
                        x => x.ScaleCode == scaleCode,
                        ct);

            if (scale is null)
            {
                scale = new ScaleConfiguration
                {
                    ScaleCode = scaleCode,

                    ScaleName =
                        request.ScaleName.Trim(),

                    MinValue =
                        request.Scores.Min(
                            x => x.MinValue!.Value),

                    MaxValue =
                        request.Scores.Max(
                            x => x.MaxValue!.Value),

                    IsActive = true
                };

                db.ScaleConfigurations.Add(scale);

                await db.SaveChangesAsync(ct);
            }
        }

        scale.ScaleName =
            request.ScaleName.Trim();

        scale.MinValue =
            request.Scores.Min(
                x => x.MinValue!.Value);

        scale.MaxValue =
            request.Scores.Max(
                x => x.MaxValue!.Value);

        scale.IsActive = true;

        // ---------------------------------------
        // Replace score bands for THIS mapping
        // ---------------------------------------

        var existingDetails =
            await db.ScaleConfigurationDetails
                .Where(x =>
                    x.IndustryParameterMappingId ==
                    request.IndustryParameterMappingId)
                .ToListAsync(ct);

        if (existingDetails.Count > 0)
        {
            db.ScaleConfigurationDetails
                .RemoveRange(existingDetails);
        }

        foreach (var item in
            request.Scores.OrderBy(x => x.Score))
        {
            db.ScaleConfigurationDetails.Add(
                new ScaleConfigurationDetails
                {
                    IndustryParameterMappingId =
                        request.IndustryParameterMappingId,

                    ScaleConfigurationId =
                        scale.ScaleConfigurationId,

                    ScoreValue =
                        item.Score,

                    Definition =
                        item.Definition?.Trim()
                        ?? string.Empty,

                    MinValue =
                        item.MinValue!.Value,

                    MaxValue =
                        item.MaxValue!.Value
                });
        }

        await db.SaveChangesAsync(ct);
        await SyncDraftJsonFromMappingsAsync(mapping.WorkflowJsonVersionId, mapping.IndustryDetailsId, ct);
    }
}