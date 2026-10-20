# CRISP 2.0 — versioned workflow/database package

## Required behavior
- One workflow/industry can have multiple versions (V1, V2, V3...).
- Every version has its own `WorkflowJsonVersionId`, `IndustryParameterMapping` rows, and scale details.
- A new draft clones the most recently configured version's mappings and scale details. Previous versions are not modified.
- Mapping retrieval is scoped by workflow code + version number + industry ID.
- Saving draft mappings synchronizes `Parameters` and `Weights` in that draft's `JsonContent`.
- Corporate Framework retrieves the explicitly selected version; an existing assessment should not silently switch versions.

## Database setup
1. Back up your database.
2. For a new database, run `Database/01_Create_CRISP_2.0_NewSchema.sql`.
3. For an existing database, run `Database/02_Migrate_IndustryParameterMapping_IndustryScope.sql` once, then `Database/03_Verify_Workflow_Version_Integrity.sql` to inspect each version.
4. Do not run the new-schema creation script over an existing production database.

## Verify the reported issue
Run `Database/03_Verify_Workflow_Version_Integrity.sql` and set `@WorkflowCode` and `@IndustryDetailsId` to your values. Each draft should show active mappings and a JSON `Parameters` array; `Weights` should be a JSON object populated from the same version's active mappings.

## Important limitation
This package contains source code and database scripts; it has not been compiled or executed in this environment because the .NET SDK and SQL Server are unavailable here. Test on a database backup before deployment.
