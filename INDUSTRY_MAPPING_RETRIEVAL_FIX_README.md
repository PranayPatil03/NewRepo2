# CRISP 2.0 — Industry-specific parameter mapping and retrieval fix

## What changed

- `IndustryParameterMapping` is now scoped by `IndustryDetailsId` as well as `WorkflowJsonVersionId` and `ParameterDefinitionMasterId`.
- Mapping retrieval and save operations filter by the selected industry, so enabling a parameter for one industry does not enable it for every industry.
- Mapping display order is persisted on the mapping row instead of being written into the shared workflow JSON.
- Mapping changes no longer rewrite the shared `WorkflowJsonVersion.JsonContent` parameter list using only one industry's selected mappings.
- Corporate's Edit/View Score dialog loads parameters using the selected industry + workflow version mapping, instead of reusing `CurrentFramework.Parameters` for every segment.
- Corporate saves/retrieves the selected industry segments with framework response JSON and reloads their matching parameter mapping IDs and scores.
- Added `GET api/framework/parameters/by-id/{parameterId}` so the UI can retrieve a parameter using the exact industry mapping ID.
- Added `Database/02_Migrate_IndustryParameterMapping_IndustryScope.sql` for existing databases.

## Existing database

1. Back up the database.
2. Run `Database/02_Migrate_IndustryParameterMapping_IndustryScope.sql` against the current CRISP database.
3. Deploy the updated API and Web projects.
4. For each industry, open Industry Parameter Mapping, select the workflow/version, configure mappings and save.
5. Open Corporate, add/select industry segments, save the draft, and reopen Edit/View Score for each industry.

The migration backfills existing mapping rows from their parent workflow version's industry. Existing rows therefore keep their prior industry assignment; configure other industries separately after migration.

## Important design note

The JSON version retains the shared workflow definition. Industry-specific enabled parameters, display order, weights and scale details are stored on the mapping rows and retrieved for the selected industry. Do not copy one industry's mapping configuration into the shared JSON parameter array, because that would overwrite the other industry's definition view.

## Build verification

The editing environment did not have the .NET SDK available, so this archive has not been compiled here. Open `CRISP_2.0.sln` in Visual Studio, restore NuGet packages, build the solution, and resolve any environment-specific connection-string or SDK configuration before deployment.
