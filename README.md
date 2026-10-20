# CRISP 2.0 — V19 UI with new-schema Clean Architecture backend

This solution preserves the V19 Web UI pages, dialogs, CSS, API routes, and RulesEngine library while replacing EF persistence with `CrispDbContext` mapped to the nine requested schema tables plus `AssessmentResponse`. The latter persists customer/entity-specific answers per immutable workflow version.

Projects: `CRISP.Domain`, `CRISP.Application`, `CRISP.Infrastructure`, `CRISP.Api`, `CRISP.Web`, and `Libraries/RulesEngine`.

Run `Database/01_Create_CRISP_2.0_NewSchema.sql`, update `src/CRISP.Api/appsettings.json`, restore packages, and build `CRISP_2.0.sln`. The current execution environment does not include the .NET SDK, so compilation could not be verified here.

Note: The V19 UI contract is kept via `FrameworkController` and `FrameworkApiClient`. Assessment answers are stored in `AssessmentResponse.ResponseJson`, unique by entity and workflow-version.
