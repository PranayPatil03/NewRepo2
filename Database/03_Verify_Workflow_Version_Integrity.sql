/* Read-only diagnostics for one industry with multiple workflow versions.
   Select the correct WorkflowCode and IndustryDetailsId before running. */
DECLARE @WorkflowCode NVARCHAR(100) = N'CorporateBusiness';
DECLARE @IndustryDetailsId INT = 1;

-- 1. JSON properties and mapping counts for every version.
SELECT
    v.WorkflowJsonVersionId,
    v.WorkflowCode,
    v.IndustryDetailsId,
    v.VersionNo,
    v.Status,
    v.IsPublished,
    ISJSON(v.JsonContent) AS IsValidJson,
    JSON_QUERY(v.JsonContent, '$[0].Parameters') AS JsonParameters,
    JSON_QUERY(v.JsonContent, '$[0].Weights') AS JsonWeights,
    JSON_QUERY(v.JsonContent, '$[0].Rules') AS JsonRules,
    COUNT(m.IndustryParameterMappingId) AS TotalMappings,
    SUM(CASE WHEN m.IsActive = 1 THEN 1 ELSE 0 END) AS ActiveMappings
FROM dbo.WorkflowJsonVersion AS v
LEFT JOIN dbo.IndustryParameterMapping AS m
    ON m.WorkflowJsonVersionId = v.WorkflowJsonVersionId
   AND m.IndustryDetailsId = v.IndustryDetailsId
WHERE v.WorkflowCode = @WorkflowCode
  AND v.IndustryDetailsId = @IndustryDetailsId
GROUP BY v.WorkflowJsonVersionId, v.WorkflowCode, v.IndustryDetailsId,
         v.VersionNo, v.Status, v.IsPublished, v.JsonContent
ORDER BY v.VersionNo;

-- 2. Version-specific mappings; each version should have its own mapping IDs.
SELECT
    v.VersionNo,
    v.Status,
    m.IndustryParameterMappingId,
    m.ParameterDefinitionMasterId,
    m.ParameterDisplayName,
    m.ValueType,
    m.Weight,
    m.DisplayOrder,
    m.IsActive
FROM dbo.WorkflowJsonVersion AS v
INNER JOIN dbo.IndustryParameterMapping AS m
    ON m.WorkflowJsonVersionId = v.WorkflowJsonVersionId
   AND m.IndustryDetailsId = v.IndustryDetailsId
WHERE v.WorkflowCode = @WorkflowCode
  AND v.IndustryDetailsId = @IndustryDetailsId
ORDER BY v.VersionNo, m.DisplayOrder;

