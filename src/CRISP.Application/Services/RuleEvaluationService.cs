using System.Text.Json;
using CRISP.Application.DTOs;
using CRISP.Application.Interfaces;
namespace CRISP.Application.Services;
public sealed class RuleEvaluationService(
IFrameworkRepository repository,
IRuleEngineExecutor ruleEngine,
IWorkflowJsonProvider jsonProvider) : IRuleEvaluationService
 {
    public async Task<EvaluateResponse> EvaluateAsync(
    EvaluateRequest request,
    CancellationToken ct = default)
     {
        var workflowJson = await jsonProvider.GetDefinitionJsonAsync(
        request.WorkflowCode,
        request.VersionNo,
        ct);
        if (string.IsNullOrWhiteSpace(workflowJson))
         {
            return new EvaluateResponse(
            false,
            0,
            request.VersionNo,
            string.Empty,
            "Workflow JSON version not found.");
        }
        // Evaluate against the selected immutable JSON version. Loading parameter definitions
        // from the live master tables here would mix the latest scores into an older version.
        WorkflowDefinitionDto? selectedDefinition;
        try
         {
            using var document = JsonDocument.Parse(workflowJson);
            var root = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().FirstOrDefault()
            : document.RootElement;
            selectedDefinition = root.ValueKind == JsonValueKind.Object
            ? root.Deserialize<WorkflowDefinitionDto>()
            : null;
        }
        catch (JsonException ex)
         {
            return new EvaluateResponse(false, 0, request.VersionNo, string.Empty,
            $"Workflow JSON is invalid: {ex.Message}");
        }
        if (selectedDefinition is null)
        return new EvaluateResponse(false, 0, request.VersionNo, string.Empty,
        "The selected workflow version has no valid definition.");
        var parameters = selectedDefinition.Parameters;
        // Validate numeric values before executing scoring rules. The versioned JSON remains
        // the immutable source of truth, while ValidationDefinition rows are persisted for admin lookup.
        foreach (var validation in selectedDefinition.Validations)
         {
            if (TryEvaluateNumericValidation(validation.Expression, request.Values, request.ManualScores, out var valid) && !valid)
            return new EvaluateResponse(false, 0, request.VersionNo, string.Empty,
            string.IsNullOrWhiteSpace(validation.ErrorMessage) ? $"Validation failed: {validation.ValidationName}" : validation.ErrorMessage);
        }
        var scores =
        new Dictionary<string, decimal>(
        StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
         {
            /*
             * Value-driven parameter
             *
             * Example:
             * MarketPositionScore = value 52
             *
             * The score is determined from the
             * ScoreDefinition stored in the database.
             */
            if (parameter.IsValueDriven &&
            request.Values.TryGetValue(
            parameter.ParameterCode,
            out var value) &&
            value.HasValue)
             {
                var matching = parameter.Scores
                .Where(x =>
                (
                !x.MinValue.HasValue ||
                (
                x.MinInclusive
                ? value.Value >= x.MinValue.Value
                : value.Value > x.MinValue.Value
                )
                )
                &&
                (
                !x.MaxValue.HasValue ||
                (
                x.MaxInclusive
                ? value.Value <= x.MaxValue.Value
                : value.Value < x.MaxValue.Value
                )
                )
                )
                .OrderBy(x => x.Score)
                .FirstOrDefault();
                if (matching is null)
                 {
                    return new EvaluateResponse(
                    false,
                    0,
                    request.VersionNo,
                    string.Empty,
                    $"No score definition in workflow V{request.VersionNo} matches value {value} for {parameter.ParameterName}.");
                }
                scores[parameter.ParameterCode] =
                matching.Score;
                continue;
            }
            /*
             * Manual score
             *
             * These values come directly from the UI.
             * Nothing is hardcoded here.
             */
            if (request.ManualScores.TryGetValue(
            parameter.ParameterCode,
            out var manualScore))
             {
                scores[parameter.ParameterCode] =
                manualScore;
                continue;
            }
            /*
             * If no value/score was supplied,
             * send zero to the Rules Engine.
             */
            scores[parameter.ParameterCode] = 0;
        }
        // The executor resolves the workflow name from this version's JSON. The DB code
        // remains the lookup key only; it need not equal the display name in WorkflowName.
        var workflowName = request.WorkflowCode;
        var result = await ruleEngine.ExecuteAsync(
        workflowJson,
        workflowName,
        scores,
        ct);
        return new EvaluateResponse(
        result.Success,
        result.Score,
        request.VersionNo,
        result.Formula,
        result.Message);
    }
    private static bool TryEvaluateNumericValidation(
    string expression,
    IReadOnlyDictionary<string, decimal?> values,
    IReadOnlyDictionary<string, int> scores,
    out bool result)
     {
        result = true;
        if (string.IsNullOrWhiteSpace(expression)) return false;
        var match = System.Text.RegularExpressions.Regex.Match(expression.Trim(),
        @"^(?:input1\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?<op>>=|<=|==|!=|>|<)\s*(?<number>-?\d+(?:\.\d+)?)$");
        if (!match.Success) return false;
        var name = match.Groups["name"].Value;
        decimal left;
        if (values.TryGetValue(name, out var inputValue) && inputValue.HasValue) left = inputValue.Value;
        else if (scores.TryGetValue(name, out var score)) left = score;
        else return false;
        if (!decimal.TryParse(match.Groups["number"].Value, System.Globalization.NumberStyles.Number,
        System.Globalization.CultureInfo.InvariantCulture, out var right)) return false;
        result = match.Groups["op"].Value switch
         {
            ">" => left > right, ">=" => left >= right, "<" => left < right, "<=" => left <= right,
            "==" => left == right, "!=" => left != right, _ => true
        };
        return true;
    }
}
