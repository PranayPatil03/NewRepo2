# CRISP 2.0 – Final Fixes Included

This package is based on the latest `CRISP_2.0_Industry_Mapping_Retrieval_Final.zip` source.

## Fixes applied
- Removed the duplicate `FrameworkController.ParameterById` action.
- Removed `IndustryDetailsId` filtering from `RuleDefinition` queries because the current `RuleDefinition` entity is scoped by `WorkflowJsonVersionId` and does not define `IndustryDetailsId`.
- Added draft-only synchronization from active industry parameter mappings into `WorkflowJsonVersion.JsonContent` after saving mappings.
- Synchronizes saved scale/score bands into each mapped JSON parameter's `Scores` array.
- Synchronizes version-scoped rules and mapping-linked validations into the JSON definition while preserving existing workflow fields.
- Added draft/read-only and industry/version consistency checks before mapping changes.
- Added JSON synchronization after score configuration saves.
- The SQL unique index remains `(WorkflowJsonVersionId, IndustryDetailsId, ParameterDefinitionMasterId)`, so the same parameter may be mapped again in another version.

## Notes
- Apply `Database/02_Migrate_IndustryParameterMapping_IndustryScope.sql` only if that schema migration has not already been applied. Back up the database first.
- Build/runtime verification was not possible in this environment because the .NET SDK is unavailable. Build the solution locally and test mapping/score updates on a Draft version before deploying.
