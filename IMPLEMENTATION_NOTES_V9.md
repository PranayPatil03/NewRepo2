# CRISP 2.0 – Workflow and Industry Configuration Updates

## Configuration flow

1. **Manage JSON**: choose an Industry first, then a workflow belonging to that industry. The selected version's current status is shown as Draft, Published, or the schema's legacy `Archieve` value.
2. **New Workflow**: choose an industry before entering the workflow code and name. The workflow is created with Draft Version 1.
3. **New Draft**: save a new version from the latest workflow definition. Existing versioned parameter mappings and scale bands are copied into the new version so edits do not overwrite the previous version.
4. **Industry Parameter Mapping**: select mapped parameters, display names, weights, calculation type (manual score or value-based), and order. Save Mapping persists the mapping and the order in that version's JSON; the database schema is unchanged because the mapping table has no `DisplayOrder` column.
5. **Score Configuration**: open this from the mapping page. Score bands use minimum/maximum input ranges and a score/definition. Value-based parameters must have configured score bands before publishing.
6. **Publish**: publish from Manage JSON after configuring mappings, score bands, rules, and validations. The publish endpoint rebuilds the final JSON from the selected version's database configuration and updates the workflow JSON provider/cache. The mapping page intentionally has no Publish Version button.

## Version and data behavior

- Published and archived versions are read-only for mapping and score edits.
- New drafts are separate version snapshots; mappings and scale details are copied to the new version.
- Parameter order is stored in version JSON rather than adding a database column, preserving the exact supplied schema.
- Value-based scoring selects the score band whose minimum/maximum range contains the entered value.
- The database's existing status spelling `Archieve` is preserved to satisfy its status check constraint.

## Validation note

The environment used to prepare this archive does not have the .NET SDK installed, so a `dotnet build` could not be run here. Review/build with the .NET 10 SDK and your configured SQL Server connection before deployment.
