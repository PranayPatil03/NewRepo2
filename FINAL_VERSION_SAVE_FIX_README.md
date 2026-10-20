# Final version-save fix

## What was wrong
`SaveWorkflowDefinitionAsync` previously used the highest version number as the source configuration. If the newest version was an empty Draft (for example V2 with `Parameters: []`), a new draft copied that empty definition and also cloned mappings from that empty draft. This could produce a new version with an empty `Parameters` array even when an earlier published version contained configuration.

## What changed
- The repository now searches versions newest-first for a source version with JSON parameters or active industry mappings.
- A blank definition is copied from that configured source version instead of blindly copying the newest version.
- Mapping and score details are cloned from that configured source version into the new version, keeping the same industry and version-specific mapping IDs.
- Parameter master lookup trims parameter codes and compares them case-insensitively.
- After version configuration is saved, the draft JSON is synchronized from the version's active mappings, scores, rules, and validations.
- Published/archived versions remain read-only; the fix does not alter older published rows.

## Existing database rows
This code fix affects new saves. It does not automatically repair an already-created empty Draft in an existing database. For an existing empty Draft, open Industry Parameter Mapping, select the same industry/workflow and that Draft version, map the required parameters, and save. Then verify `JsonContent` and the version-specific mappings before publishing.

## Verification query
```sql
SELECT v.WorkflowJsonVersionId, v.VersionNo, v.Status, v.IsPublished,
       JSON_QUERY(v.JsonContent, '$[0].Parameters') AS JsonParameters,
       COUNT(m.IndustryParameterMappingId) AS TotalMappings,
       SUM(CASE WHEN m.IsActive = 1 THEN 1 ELSE 0 END) AS ActiveMappings
FROM dbo.WorkflowJsonVersion AS v
LEFT JOIN dbo.IndustryParameterMapping AS m
  ON m.WorkflowJsonVersionId = v.WorkflowJsonVersionId
 AND m.IndustryDetailsId = v.IndustryDetailsId
WHERE v.WorkflowCode = 'CorporateBusiness'
GROUP BY v.WorkflowJsonVersionId, v.VersionNo, v.Status, v.IsPublished, v.JsonContent
ORDER BY v.VersionNo DESC;
```

A .NET SDK was not available in the packaging environment, so this archive has not been build-verified here. Build the solution in Visual Studio and resolve any environment-specific package or connection-string issues before deployment.
