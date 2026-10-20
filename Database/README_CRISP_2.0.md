# CRISP 2.0 database

The active application uses `CrispDbContext` and only these configuration tables: `IndustryDetails`, `CustomerMaster`, `ParameterDefinitionMaster`, `WorkflowJsonVersion`, `IndustryParameterMapping`, `ValidationDefinition`, `RuleDefinition`, `ScaleConfiguration`, and `ScaleConfigurationDetails`.

`AssessmentResponse` is the one additional table required for V19-style entity-specific assessment answer persistence. The workflow definition JSON remains versioned in `WorkflowJsonVersion.JsonContent`; each answer set is stored in `AssessmentResponse.ResponseJson`, keyed by entity and workflow version.

Run `01_Create_CRISP_2.0_NewSchema.sql` in SSMS. This is a Database-First project and does not use EF migrations.
