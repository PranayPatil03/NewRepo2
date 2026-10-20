# V19 exact seed data on the new schema

`01_Create_CRISP_2.0_NewSchema.sql` seeds the data from the V19 reference scripts, translated to the new schema. It retains the original entity names, industry names, parameter codes and descriptions, weights, scoring bands, workflow JSON expressions, version names/statuses, and initial assessment values.

## V19 seed entities
- 20 Microns Limited
- 20 Microns Minerals, Foods Limited-Test 10
- 20 Microns Nano Minerals Limited

## Workflow versions
- `CorporateBusiness`, Version 1 — `09-Sep-2026 - Draft`, Published, weights 15/15/25/25/20.
- `CorporateBusiness`, Version 2 — `03-Sep-2026`, Draft, weights 20/10/25/25/20.

## Framework names represented in the initial assessment snapshots
- 09-Sep-2026 - Draft
- 03-Sep-2026
- 25-Aug-2026
- 24-Aug-2026 17:48
- 24-Aug-2026 16:09
- 19-Aug-2026 16:31
- 19-Aug-2026 11:34
- 06-Aug-2026
- 05-Aug-2026 15:45

The original V19 schema used `FrameworkInstance` and `FrameworkParameterValue`. Those tables are not recreated. Historical framework labels and values are stored in `AssessmentResponse.ResponseJson`, and the old one-response-per-entity/version unique constraint is removed so multiple saved snapshots can exist for the same entity and workflow version.

The V19 scripts do not seed any `ValidationDefinition` rows, so this script does not invent validation rows.
