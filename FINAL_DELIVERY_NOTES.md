# CRISP 2.0 — final versioned workflow package

## Requirements covered
- One industry can have multiple versions under the same workflow code.
- Each version has its own `WorkflowJsonVersionId`, JSON definition, industry mappings, and version-specific score details.
- New-version creation searches backward for the newest configured source version instead of blindly cloning an empty latest Draft.
- The source industry's ID is preserved; this package does not require adding `IndustryDetailsId` to a request DTO to create another version.
- Draft JSON is synchronized from active mappings and score details after configuration is saved.
- Published/archived versions remain read-only in the mapping/JSON save flow.

## Database files
- `Database/01_Create_CRISP_2.0_NewSchema.sql` — creates and seeds the new schema. **This is a reset/create script and drops existing tables; use only on a new/test database after backup.**
- `Database/02_Migrate_IndustryParameterMapping_IndustryScope.sql` — migration for an existing database missing `IndustryParameterMapping.IndustryDetailsId`. Run once after backup, before deploying the matching code. Do not run the new-schema reset script on UAT data.
- `Database/03_Verify_Workflow_Version_Integrity.sql` — read-only diagnostics for JSON fields and version-specific mappings. Change the workflow code/industry ID variables if needed.

## Applying to the existing UAT database
1. Back up `CRISP2_UAT`.
2. Confirm `dbo.IndustryParameterMapping.IndustryDetailsId` exists. The screenshots show that it does. Do not rerun the migration if its column/index already exists; inspect it first.
3. Deploy the updated application binaries/source.
4. Open the affected Draft version in Industry Parameter Mapping and save the mappings. The repository's `SyncDraftJsonFromMappingsAsync` writes the selected Draft's `Parameters`, `Weights`, rules, and validations from that version's rows. Then run `03_Verify_Workflow_Version_Integrity.sql`.
5. Confirm the draft JSON is populated and weights are valid before publishing. Do not edit published JSON directly.

## Build/verification status
The source archive was updated and packaged. A .NET SDK was not available in the packaging environment, so a fresh `dotnet build` and SQL execution against your UAT database could not be performed here. Build in Visual Studio and run the verification SQL against a backup/test database before deployment.
