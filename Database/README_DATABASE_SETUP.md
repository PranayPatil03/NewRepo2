# CRISP 2.0 database setup (new schema)

1. Open SQL Server Management Studio.
2. Run `Database/01_Create_CRISP_2.0_NewSchema.sql`.
3. Update `src/CRISP.Api/appsettings.json` with your SQL Server connection string.
4. Build and run the API, then start the Web project.

The schema contains the nine requested configuration tables plus `AssessmentResponse`, which stores entity-specific answers by immutable workflow version. No EF Core migrations are used. Do not run historical V19 upgrade scripts against this database.
