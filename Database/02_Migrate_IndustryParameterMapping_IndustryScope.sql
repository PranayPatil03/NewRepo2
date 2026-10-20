/* CRISP 2.0 migration: isolate parameter mappings by industry within a workflow JSON version.
   Run once against an existing database before deploying the updated application. */
IF COL_LENGTH('dbo.IndustryParameterMapping', 'IndustryDetailsId') IS NULL
BEGIN
    ALTER TABLE dbo.IndustryParameterMapping ADD IndustryDetailsId INT NULL;

    UPDATE m
       SET IndustryDetailsId = v.IndustryDetailsId
      FROM dbo.IndustryParameterMapping m
      INNER JOIN dbo.WorkflowJsonVersion v
        ON v.WorkflowJsonVersionId = m.WorkflowJsonVersionId
     WHERE m.IndustryDetailsId IS NULL;

    ALTER TABLE dbo.IndustryParameterMapping ALTER COLUMN IndustryDetailsId INT NOT NULL;

    ALTER TABLE dbo.IndustryParameterMapping ADD CONSTRAINT FK_IPM_IndustryDetails
        FOREIGN KEY (IndustryDetailsId) REFERENCES dbo.IndustryDetails(IndustryDetailId);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_IPM_Version_Industry_Parameter' AND object_id = OBJECT_ID('dbo.IndustryParameterMapping'))
    CREATE UNIQUE INDEX UX_IPM_Version_Industry_Parameter
        ON dbo.IndustryParameterMapping(WorkflowJsonVersionId, IndustryDetailsId, ParameterDefinitionMasterId);
GO

IF COL_LENGTH('dbo.IndustryParameterMapping', 'DisplayOrder') IS NULL
BEGIN
    ALTER TABLE dbo.IndustryParameterMapping ADD DisplayOrder INT NOT NULL CONSTRAINT DF_IndustryParameterMapping_DisplayOrder DEFAULT(9999);
END;
GO
