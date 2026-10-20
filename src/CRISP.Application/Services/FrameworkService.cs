using System.Text.Json;
using CRISP.Application.DTOs;
using CRISP.Application.Interfaces;
namespace CRISP.Application.Services;

public sealed class FrameworkService(
IFrameworkRepository repository,
IWorkflowJsonProvider jsonProvider) : IFrameworkService
{
    public Task<IReadOnlyList<WorkflowDto>> GetWorkflowsAsync(CancellationToken ct = default) => repository.GetWorkflowsAsync(ct);
    public async Task<WorkflowDto> CreateWorkflowAsync(
    CreateWorkflowRequest request,
    CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowCode))
            throw new InvalidOperationException("Workflow code is required.");
        if (string.IsNullOrWhiteSpace(request.WorkflowName))
            throw new InvalidOperationException("Workflow name is required.");
        var definition = new WorkflowDefinitionDto
        {
            WorkflowName = request.WorkflowName.Trim()
        };
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null
        };
        var skeletonJson = JsonSerializer.Serialize(
        new[]  {
            definition
        },
        jsonOptions);
        var workflow = await repository.CreateWorkflowAsync(
        request,
        skeletonJson,
        ct);
        jsonProvider.SetDefinitionJson(
        request.WorkflowCode.Trim(),
        1,
        skeletonJson);
        return workflow;
    }
    public Task<WorkflowContextDto?> GetWorkflowContextAsync(string workflowCode, CancellationToken ct = default) => repository.GetWorkflowContextAsync(workflowCode, ct);
    public Task<IReadOnlyList<EntityDto>> SearchEntitiesAsync(string? search, CancellationToken ct = default) => repository.SearchEntitiesAsync(search, ct);
    public Task<IReadOnlyList<FrameworkLookupDto>> GetFrameworkOptionsAsync(int entityId, CancellationToken ct = default) => repository.GetFrameworkOptionsAsync(entityId, ct);
    public Task<IReadOnlyList<IndustrySegmentDto>> GetIndustrySegmentsAsync(string workflowCode, CancellationToken ct = default) => repository.GetIndustrySegmentsAsync(workflowCode, ct);
    public Task<IReadOnlyList<FrameworkStepDto>> GetStepsAsync(string workflowCode, CancellationToken ct = default) => repository.GetStepsAsync(workflowCode, ct);
    public Task<IReadOnlyList<FrameworkListDto>> GetFrameworksAsync(CancellationToken ct = default) => repository.GetFrameworksAsync(ct);
    public Task<IReadOnlyList<ParameterDto>> GetParametersAsync(string workflowCode, CancellationToken ct = default) => repository.GetParametersAsync(workflowCode, ct);
    public Task<ParameterDto?> GetParameterAsync(int parameterId, CancellationToken ct = default) => repository.GetParameterAsync(parameterId, ct);
    

    public Task<IReadOnlyList<ParameterDto>> GetParameterMastersAsync(
    CancellationToken ct = default)
    => repository.GetParameterMastersAsync(ct);

    public Task<ParameterDto> SaveParameterMasterAsync(
        ParameterDto request,
        CancellationToken ct = default)
        => repository.SaveParameterMasterAsync(request, ct);
    public Task<IReadOnlyList<VersionDto>> GetVersionsAsync(string workflowCode, CancellationToken ct = default) => repository.GetVersionsAsync(workflowCode, ct);
    public Task<VersionDto?> GetVersionAsync(
    string workflowCode,
    int versionNo,
    CancellationToken ct = default)
    {
        return repository.GetVersionAsync(
            workflowCode,
            versionNo,
            ct);
    }
    public async Task<FrameworkDto?> GetFrameworkAsync(int id, int? versionNo = null, CancellationToken ct = default)
    {
        // Edit/View is JSON-first. Assessment answers are loaded from AssessmentResponse only when needed.
        var framework = await repository.GetFrameworkShellAsync(id, ct);
        if (framework is null)
            return null;
        // The latest published version is the default. A requested older version is loaded only
        // when the user explicitly selects it in the version dropdown.
        var versions = await repository.GetVersionsAsync(framework.WorkflowCode, ct);
        var targetVersion = versionNo
        ?? versions.Where(x => x.IsPublished).OrderByDescending(x => x.VersionNo).Select(x => (int?)x.VersionNo).FirstOrDefault()
        ?? framework.WorkflowVersionNo;
        framework.WorkflowVersionNo = targetVersion;
        var valueJson = await repository.GetFrameworkValueJsonAsync(id, targetVersion, ct);
        var version = await repository.GetVersionAsync(framework.WorkflowCode, targetVersion, ct);
        var definitionJson = version?.JsonContent ?? await jsonProvider.GetDefinitionJsonAsync(
        framework.WorkflowCode,
        targetVersion,
        ct);
        // Build the visible parameter list from the selected immutable JSON snapshot.
        // Parameter master rows are only used to map stable codes to database IDs.
        ApplyVersionParameters(framework, definitionJson);
        // Recover an existing assessment response lazily: if a version has no value snapshot yet,
        // copy matching values from the the saved AssessmentResponse values once, then
        // persist them under the selected version. Newly added parameters remain blank.
        if (string.IsNullOrWhiteSpace(valueJson) && !string.IsNullOrWhiteSpace(definitionJson))
        {
            var savedValues = await repository.GetFrameworkParameterValuesAsync(id, ct);
            framework.ParameterValues = framework.Parameters.Select(parameter =>
            {
                var saved = savedValues.FirstOrDefault(value => value.ParameterId == parameter.Id)
                ?? savedValues.FirstOrDefault(value => value.ParameterName.Equals(parameter.ParameterName, StringComparison.OrdinalIgnoreCase));
                return new FrameworkParameterDto(
                parameter.Id,
                parameter.ParameterName,
                parameter.Weight,
                saved?.Value,
                saved?.Score ?? 0);
            }).ToList();
            valueJson = BuildValueJson(framework);
            await repository.SaveFrameworkValueJsonAsync(id, targetVersion, valueJson, ct);
            jsonProvider.InvalidateValue(id);
        }
        if (TryBuildValuesFromJson(framework, definitionJson, valueJson, out var jsonValues, out var error))
        {
            framework.ParameterValues = jsonValues;
            framework.ValueSource = "JSON Cache";
            framework.JsonStatus = "Valid";
            return framework;
        }
        // JSON is unavailable, invalid or inconsistent. Only now read the relational values.
        // Do not fall back to the live relational values for an older version: those values may
        // belong to a different configuration. Database fallback is safe only for the framework's
        // currently stored version.
        framework.ParameterValues = targetVersion == (await repository.GetFrameworkShellAsync(id, ct))?.WorkflowVersionNo
        ? (await repository.GetFrameworkParameterValuesAsync(id, ct)).Where(v => framework.Parameters.Any(p => p.Id == v.ParameterId)).ToList()
        : framework.Parameters.Select(p => new FrameworkParameterDto(p.Id, p.ParameterName, p.Weight, null, 0)).ToList();
        framework.ValueSource = targetVersion == (await repository.GetFrameworkShellAsync(id, ct))?.WorkflowVersionNo ? "Database Fallback" : "No saved values for selected version";
        framework.JsonStatus = string.IsNullOrWhiteSpace(error)
        ? "JSON failed"
        : $"JSON failed: {error}";
        return framework;
    }
    private static void ApplyVersionParameters(FrameworkDto framework, string? definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson)) return;
        try
        {
            using var doc = JsonDocument.Parse(definitionJson);
            var root = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().FirstOrDefault()
            : doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array) return;
            var definitions = JsonSerializer.Deserialize<List<WorkflowJsonParameterDto>>(parameters.GetRawText()) ?? [];
            var current = framework.Parameters.ToDictionary(x => x.ParameterCode, StringComparer.OrdinalIgnoreCase);
            var versionParameters = new List<ParameterDto>();
            foreach (var definition in definitions)
            {
                if (!current.TryGetValue(definition.ParameterCode, out var master)) continue;
                var scores = definition.Scores.Select(x => new ScoreDto(x.Score, x.Definition, x.ThresholdValue, x.Unit, x.MinValue, x.MaxValue, x.MinInclusive, x.MaxInclusive)).ToList();
                versionParameters.Add(new ParameterDto(master.Id, definition.ParameterCode, definition.ParameterName, definition.WhyItMatters, definition.HowDoWeMeasure, definition.Weight, definition.DisplayOrder, definition.IsValueDriven, scores));
            }
            framework.Parameters = versionParameters;
        }
        catch (JsonException)
        {
            /* Keep the existing parameter list if an old JSON schema cannot be parsed. */
        }
    }
    public async Task<CalculateScoreResponse?> CalculateScoreAsync(int parameterId, decimal? value, CancellationToken ct = default)
    {
        var parameter = await repository.GetParameterAsync(parameterId, ct);
        if (parameter is null) return null;
        if (!parameter.IsValueDriven || value is null)
        {
            var first = parameter.Scores.OrderBy(x => x.Score).FirstOrDefault();
            return first is null ? null : new CalculateScoreResponse(parameter.Id, first.Score, first.Definition);
        }
        var match = parameter.Scores
        .Where(x => (!x.MinValue.HasValue || (x.MinInclusive ? value.Value >= x.MinValue.Value : value.Value > x.MinValue.Value))
        && (!x.MaxValue.HasValue || (x.MaxInclusive ? value.Value <= x.MaxValue.Value : value.Value < x.MaxValue.Value)))
        .OrderBy(x => x.Score)
        .FirstOrDefault();
        return match is null ? null : new CalculateScoreResponse(parameter.Id, match.Score, match.Definition);
    }
    public async Task<FrameworkDto> SaveFrameworkAsync(FrameworkDto framework, CancellationToken ct = default)
    {
        // Score using the selected version's score definitions, not the latest master scores.
        var parameterMap = framework.Parameters.ToDictionary(x => x.Id);
        framework.ParameterValues = framework.ParameterValues.Select(item =>
        {
            if (!parameterMap.TryGetValue(item.ParameterId, out var definition) || !definition.IsValueDriven || !item.Value.HasValue)
                return item;
            var match = definition.Scores
            .Where(x => (!x.MinValue.HasValue || (x.MinInclusive ? item.Value.Value >= x.MinValue.Value : item.Value.Value > x.MinValue.Value))
            && (!x.MaxValue.HasValue || (x.MaxInclusive ? item.Value.Value <= x.MaxValue.Value : item.Value.Value < x.MaxValue.Value)))
            .OrderBy(x => x.Score)
            .FirstOrDefault();
            return match is null
            ? new FrameworkParameterDto(item.ParameterId, item.ParameterName, item.Weight, item.Value, 0)
            : new FrameworkParameterDto(item.ParameterId, item.ParameterName, item.Weight, item.Value, match.Score);
        }).ToList();
        var saved = await repository.SaveFrameworkAsync(framework, ct);
        // Repository relational rows represent the framework's default version. Preserve the
        // user's selected-version payload here so the version-specific JSON snapshot gets the
        // values they just saved, even when an older version was selected.
        saved.WorkflowVersionNo = framework.WorkflowVersionNo;
        saved.ParameterValues = framework.ParameterValues.ToList();
        saved.Parameters = framework.Parameters.ToList();
        var hasValues = saved.ParameterValues.Any(x => x.Value.HasValue || x.Score != 0);
        // Save even an all-blank value set. Otherwise a previous snapshot for this version could
        // reappear when the user retrieves it later.
        var valueJson = BuildValueJson(saved);
        await repository.SaveFrameworkValueJsonAsync(saved.Id, saved.WorkflowVersionNo, valueJson, ct);
        jsonProvider.InvalidateValue(saved.Id);
        saved.ValueSource = hasValues ? "Versioned JSON Snapshot" : "Versioned JSON Snapshot (blank values)";
        saved.JsonStatus = "Valid";
        return saved;
    }
    public async Task<VersionDto> SaveVersionAsync(SaveVersionRequest request, CancellationToken ct = default)
    {
        var version = await repository.SaveVersionAsync(request, ct);
        jsonProvider.InvalidateDefinition(request.WorkflowCode, version.VersionNo);
        return version;
    }
    public Task<WorkflowDefinitionDto?> GetWorkflowDefinitionAsync(string workflowCode, CancellationToken ct = default) =>
    repository.GetWorkflowDefinitionAsync(workflowCode, ct);
    public async Task<VersionDto> SaveWorkflowDefinitionAsync(SaveWorkflowDefinitionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowCode))
            throw new InvalidOperationException("Workflow code is required.");
        var weight = request.Definition.Parameters.Sum(x => x.Weight);
        if (request.Definition.Parameters.Count > 0 && weight <= 0)
            throw new InvalidOperationException("Parameter weights must be greater than zero.");
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null
        };
        var json = JsonSerializer.Serialize(new[]  {
            request.Definition
        }, jsonOptions);
        request.VersionName = string.IsNullOrWhiteSpace(request.VersionName)
        ? $"{DateTime.Today:dd-MMM-yyyy} - Draft"
        : request.VersionName.Trim();
        var version = await repository.SaveWorkflowDefinitionAsync(request, json, ct);
        jsonProvider.SetDefinitionJson(request.WorkflowCode, version.VersionNo, json);
        return version;
    }
    private static string BuildValueJson(FrameworkDto framework)
    {
        var snapshot = new FrameworkValueSnapshotJson
        {
            FrameworkId = framework.Id,
            WorkflowCode = framework.WorkflowCode,
            WorkflowVersionNo = framework.WorkflowVersionNo,
            EntityId = framework.EntityId,
            EntityName = framework.EntityName,
            Values = framework.ParameterValues.Select(x => new FrameworkValueJsonItem
            {
                ParameterId = x.ParameterId,
                ParameterCode = framework.Parameters.FirstOrDefault(p => p.Id == x.ParameterId)?.ParameterCode ?? string.Empty,
                ParameterName = x.ParameterName,
                Weight = x.Weight,
                Value = x.Value,
                Score = x.Score
            }).ToList()
        };
        return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
    private static bool TryBuildValuesFromJson(
    FrameworkDto framework,
    string? definitionJson,
    string? valueJson,
    out List<FrameworkParameterDto> values,
    out string error)
    {
        values = [];
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            error = "definition JSON not found";
            return false;
        }
        if (string.IsNullOrWhiteSpace(valueJson))
        {
            error = "value JSON not found";
            return false;
        }
        try
        {
            var definitions = ParseDefinition(definitionJson);
            var snapshot = JsonSerializer.Deserialize<FrameworkValueSnapshotJson>(valueJson);
            if (snapshot is null)
            {
                error = "value JSON could not be parsed";
                return false;
            }
            if (!string.Equals(snapshot.WorkflowCode, framework.WorkflowCode, StringComparison.OrdinalIgnoreCase)
            || snapshot.FrameworkId != framework.Id
            || snapshot.WorkflowVersionNo != framework.WorkflowVersionNo)
            {
                error = "value JSON does not match the selected framework/version";
                return false;
            }
            var snapshotValues = snapshot.Values
            .Where(x => !string.IsNullOrWhiteSpace(x.ParameterCode)
            || !string.IsNullOrWhiteSpace(x.ParameterName)
            || x.ParameterId > 0)
            .ToList();
            // The parameter set may differ between versions. Match by stable code first,
            // then by legacy name/ID, and initialize newly introduced parameters as blank.
            foreach (var definition in definitions.Parameters)
            {
                var savedParameter = framework.Parameters.FirstOrDefault(p =>
                (!string.IsNullOrWhiteSpace(definition.ParameterCode)
                && p.ParameterCode.Equals(definition.ParameterCode, StringComparison.OrdinalIgnoreCase))
                || p.ParameterName.Equals(definition.ParameterName, StringComparison.OrdinalIgnoreCase));
                // Match by stable parameter code first. Names and database IDs are legacy
                // fallbacks only; OR-ing all keys together can select a different parameter
                // when definitions were reordered or renamed between versions.
                var item = snapshotValues.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(definition.ParameterCode)
                && !string.IsNullOrWhiteSpace(x.ParameterCode)
                && x.ParameterCode.Equals(definition.ParameterCode, StringComparison.OrdinalIgnoreCase));
                item ??= snapshotValues.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.ParameterName)
                && x.ParameterName.Equals(definition.ParameterName, StringComparison.OrdinalIgnoreCase));
                item ??= savedParameter is null
                ? null
                : snapshotValues.FirstOrDefault(x => x.ParameterId == savedParameter.Id);
                values.Add(new FrameworkParameterDto(
                savedParameter?.Id ?? definition.ParameterId,
                definition.ParameterName,
                definition.Weight,
                item?.Value,
                item?.Score ?? 0));
            }
            return true;
        }
        catch (JsonException ex)
        {
            values.Clear();
            error = ex.Message;
            return false;
        }
    }
    private static WorkflowDefinitionForCompare ParseDefinition(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.ValueKind == JsonValueKind.Array
        ? document.RootElement.EnumerateArray().FirstOrDefault()
        : document.RootElement;
        var parameters = root.TryGetProperty("Parameters", out var parameterElement)
        ? parameterElement.Deserialize<List<ParameterForCompare>>() ?? []
        : [];
        return new WorkflowDefinitionForCompare(parameters);
    }

    public Task<IReadOnlyList<IndustryParameterMappingDto>>
    GetIndustryParameterMappingsAsync(
        string workflowCode,
        int versionNo,
        int industryId,
        CancellationToken ct = default)
    {
        return repository.GetIndustryParameterMappingsAsync(
            workflowCode,
            versionNo,
            industryId,
            ct);
    }

    public async Task<bool> SaveIndustryParameterMappingsAsync(
    SaveIndustryParameterMappingsRequest request,
    CancellationToken ct = default)
    {
        // 1. Validate workflow
        if (string.IsNullOrWhiteSpace(request.WorkflowCode))
        {
            throw new InvalidOperationException(
                "Workflow code is required.");
        }

        // 2. Validate version
        if (request.VersionNo <= 0)
        {
            throw new InvalidOperationException(
                "Version number is required.");
        }

        // 3. Validate industry
        if (request.IndustryDetailsId <= 0)
        {
            throw new InvalidOperationException(
                "Industry is required.");
        }

        // 4. Validate mappings
        if (request.Mappings is null)
        {
            throw new InvalidOperationException(
                "Mappings are required.");
        }

        // 5. Get exact selected version
        var version = await repository.GetWorkflowVersionAsync(
            request.WorkflowCode,
            request.VersionNo,
            ct);

        if (version is null)
        {
            throw new InvalidOperationException(
                $"Workflow '{request.WorkflowCode}' " +
                $"V{request.VersionNo} was not found.");
        }

        // 7. Published versions cannot be modified
        if (version.IsPublished ||
            version.Status.Equals(
                "Published",
                StringComparison.OrdinalIgnoreCase) ||
            version.Status.Equals(
                "Archieve",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Version V{request.VersionNo} is read-only.");
        }

        var selectedMappings = request.Mappings
            .Where(x => x.IsMapped)
            .ToList();

        // 8. At least one parameter
        if (selectedMappings.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one parameter must be mapped.");
        }

        // 9. Check duplicate parameters
        var duplicateIds = selectedMappings
            .GroupBy(x => x.ParameterDefinitionMasterId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            throw new InvalidOperationException(
                "The same parameter cannot be mapped more than once.");
        }

        // 10. Negative weights not allowed
        if (selectedMappings.Any(x => x.Weight < 0))
        {
            throw new InvalidOperationException(
                "Parameter weight cannot be negative.");
        }

        // 11. Validate total
        var totalWeight = selectedMappings.Sum(x => x.Weight);

        if (Math.Abs(totalWeight - 100m) > 0.01m)
        {
            throw new InvalidOperationException(
                $"Mapped parameter weight is " +
                $"{totalWeight:0.##}%. " +
                "Total weight must be 100%.");
        }

        // 12. Fix missing display order
        var order = 1;

        foreach (var mapping in selectedMappings)
        {
            if (mapping.DisplayOrder <= 0)
            {
                mapping.DisplayOrder = order;
            }

            order++;
        }

        // 13. Persistence
        await repository.SaveIndustryParameterMappingsAsync(
            version.WorkflowJsonVersionId,
            request.IndustryDetailsId,
            request.Mappings,
            ct);

        return true;
    }

    public async Task<VersionDto> PublishVersionAsync(
    string workflowCode,
    int versionNo,
    CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflowCode))
        {
            throw new InvalidOperationException(
                "Workflow code is required.");
        }

        if (versionNo <= 0)
        {
            throw new InvalidOperationException(
                "Version number is required.");
        }

        var version = await repository.GetWorkflowVersionAsync(
            workflowCode,
            versionNo,
            ct);

        if (version is null)
        {
            throw new InvalidOperationException(
                $"Workflow '{workflowCode}' V{versionNo} was not found.");
        }

        if (version.IsPublished ||
            version.Status.Equals(
                "Published",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Version V{versionNo} is already published.");
        }

        if (version.Status.Equals(
            "Archieve",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Archieve version V{versionNo} cannot be published.");
        }

        var mappings =
            await repository.GetIndustryParameterMappingsAsync(
                workflowCode,
                versionNo,
                version.IndustryDetailsId,
                ct);

        var selectedMappings = mappings
            .Where(x => x.IsMapped)
            .ToList();

        if (selectedMappings.Count == 0)
        {
            throw new InvalidOperationException(
                "Cannot publish this version because " +
                "no parameters are mapped.");
        }

        var totalWeight =
            selectedMappings.Sum(x => x.Weight);

        if (Math.Abs(totalWeight - 100m) > 0.01m)
        {
            throw new InvalidOperationException(
                $"Cannot publish V{versionNo}. " +
                $"Mapped parameter weight is {totalWeight:0.##}%. " +
                "Total weight must be 100%.");
        }

        // Value-driven parameters must have persisted score bands before publishing.
        foreach (var mapping in selectedMappings.Where(x =>
                     x.ValueType.Equals("Value", StringComparison.OrdinalIgnoreCase)))
        {
            var scoreConfiguration = await repository.GetScoreConfigurationAsync(
                mapping.IndustryParameterMappingId, ct);
            if (scoreConfiguration is null || scoreConfiguration.Scores.Count == 0 ||
                scoreConfiguration.Scores.Any(x => !x.MinValue.HasValue || !x.MaxValue.HasValue))
            {
                throw new InvalidOperationException(
                    $"Configure minimum/maximum value conditions and scores for '{mapping.ParameterDisplayName}' before publishing.");
            }
        }

        // ============================================================
        // BUILD FINAL DEFINITION FROM DATABASE
        // ============================================================

        var definition =
            await repository.BuildWorkflowDefinitionAsync(
                workflowCode,
                versionNo,
                ct);

        if (definition is null)
        {
            throw new InvalidOperationException(
                $"Workflow definition for '{workflowCode}' " +
                $"V{versionNo} could not be generated.");
        }

        // ============================================================
        // GENERATE FINAL JSON
        // ============================================================

        var jsonOptions =
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null
            };

        var jsonContent =
            JsonSerializer.Serialize(
                new[] { definition },
                jsonOptions);

        // ============================================================
        // SAVE JSON + PUBLISH
        // ============================================================

        var published =
            await repository.PublishVersionAsync(
                workflowCode,
                versionNo,
                jsonContent,
                ct);

        if (published is null)
        {
            throw new InvalidOperationException(
                "Workflow version could not be published.");
        }

        // Keep JSON provider/cache synchronized.
        jsonProvider.SetDefinitionJson(
            workflowCode,
            versionNo,
            jsonContent);

        return new VersionDto(
            published.WorkflowJsonVersionId,
            published.VersionNo,
            published.VersionName,
            published.Status,
            published.IsPublished,
            DateTime.UtcNow,
            "Admin");
    }

    public Task<IReadOnlyList<IndustrySegmentDto>>
    GetAllIndustriesAsync(
        CancellationToken ct = default)
    {
        return repository.GetAllIndustriesAsync(ct);
    }

    public Task<ScoreConfigurationDto?> GetScoreConfigurationAsync(
    int industryParameterMappingId,
    CancellationToken ct = default)
    {
        return repository.GetScoreConfigurationAsync(
            industryParameterMappingId,
            ct);
    }

    public Task SaveScoreConfigurationAsync(
        ScoreConfigurationDto request,
        CancellationToken ct = default)
    {
        return repository.SaveScoreConfigurationAsync(
            request,
            ct);
    }



    private sealed record WorkflowDefinitionForCompare(List<ParameterForCompare> Parameters);
    private sealed class ParameterForCompare
    {
        public int ParameterId
        {
            get;
            set;
        }
        public string ParameterCode
        {
            get;
            set;
        }
        = string.Empty;
        public string ParameterName
        {
            get;
            set;
        }
        = string.Empty;
        public decimal Weight
        {
            get;
            set;
        }
    }
    private sealed class FrameworkValueSnapshotJson
    {
        public int FrameworkId
        {
            get;
            set;
        }
        public string WorkflowCode
        {
            get;
            set;
        }
        = string.Empty;
        public int WorkflowVersionNo
        {
            get;
            set;
        }
        public int EntityId
        {
            get;
            set;
        }
        public string EntityName
        {
            get;
            set;
        }
        = string.Empty;
        public List<FrameworkValueJsonItem> Values
        {
            get;
            set;
        }
        = [];
    }
    private sealed class FrameworkValueJsonItem
    {
        public int ParameterId
        {
            get;
            set;
        }
        public string ParameterCode
        {
            get;
            set;
        }
        = string.Empty;
        public string ParameterName
        {
            get;
            set;
        }
        = string.Empty;
        public decimal Weight
        {
            get;
            set;
        }
        public decimal? Value
        {
            get;
            set;
        }
        public int Score
        {
            get;
            set;
        }
    }
}