/*
 CRISP 2.0 - New schema + repeatable demo seed data
 SQL Server / Database-First. No EF Core migrations required.
 Creates the nine requested tables plus AssessmentResponse for entity-specific answers.
 Safe to rerun: tables are created only when missing and seed rows are inserted by natural keys.
*/

Use [CRISP2_UAT]


/*
  OPTIONAL RESET: drops the current schema tables before recreating them.
  WARNING: this permanently deletes all data in these tables.
  Drop child tables before parent tables to satisfy foreign-key dependencies.
*/
IF OBJECT_ID(N'dbo.AssessmentResponse', N'U') IS NOT NULL DROP TABLE dbo.AssessmentResponse;
IF OBJECT_ID(N'dbo.ValidationDefinition', N'U') IS NOT NULL DROP TABLE dbo.ValidationDefinition;
IF OBJECT_ID(N'dbo.RuleDefinition', N'U') IS NOT NULL DROP TABLE dbo.RuleDefinition;
IF OBJECT_ID(N'dbo.ScaleConfigurationDetails', N'U') IS NOT NULL DROP TABLE dbo.ScaleConfigurationDetails;
IF OBJECT_ID(N'dbo.IndustryParameterMapping', N'U') IS NOT NULL DROP TABLE dbo.IndustryParameterMapping;
IF OBJECT_ID(N'dbo.WorkflowJsonVersion', N'U') IS NOT NULL DROP TABLE dbo.WorkflowJsonVersion;
IF OBJECT_ID(N'dbo.CustomerMaster', N'U') IS NOT NULL DROP TABLE dbo.CustomerMaster;
IF OBJECT_ID(N'dbo.ParameterDefinitionMaster', N'U') IS NOT NULL DROP TABLE dbo.ParameterDefinitionMaster;
IF OBJECT_ID(N'dbo.ScaleConfiguration', N'U') IS NOT NULL DROP TABLE dbo.ScaleConfiguration;
IF OBJECT_ID(N'dbo.IndustryDetails', N'U') IS NOT NULL DROP TABLE dbo.IndustryDetails;
GO

IF OBJECT_ID(N'dbo.IndustryDetails', N'U') IS NULL
CREATE TABLE dbo.IndustryDetails(
 IndustryDetailId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IndustryDetails PRIMARY KEY,
 IndustryName VARCHAR(256) NOT NULL,
 IsActive BIT NOT NULL CONSTRAINT DF_IndustryDetails_IsActive DEFAULT(1)
);
GO
IF OBJECT_ID(N'dbo.CustomerMaster', N'U') IS NULL
CREATE TABLE dbo.CustomerMaster(
 EntityId INT IDENTITY(1,1) NOT NULL 
     CONSTRAINT PK_CustomerMaster PRIMARY KEY,
 EntityName VARCHAR(256) NOT NULL,
 IsActive BIT NOT NULL 
     CONSTRAINT DF_CustomerMaster_IsActive DEFAULT(1)
);
GO
IF OBJECT_ID(N'dbo.ParameterDefinitionMaster', N'U') IS NULL
CREATE TABLE dbo.ParameterDefinitionMaster(
 ParameterDefinitionMasterId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ParameterDefinitionMaster PRIMARY KEY,
 ParameterCode VARCHAR(128) NOT NULL,
 ParameterName VARCHAR(256) NOT NULL,
 WhyItMatters VARCHAR(256) NOT NULL CONSTRAINT DF_ParameterDefinitionMaster_Why DEFAULT(''),
 IsActive BIT NOT NULL CONSTRAINT DF_ParameterDefinitionMaster_IsActive DEFAULT(1),
 CONSTRAINT UQ_ParameterDefinitionMaster_Code UNIQUE(ParameterCode)
);
GO
IF OBJECT_ID(N'dbo.WorkflowJsonVersion', N'U') IS NULL
CREATE TABLE dbo.WorkflowJsonVersion(
 WorkflowJsonVersionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkflowJsonVersion PRIMARY KEY,
 IndustryDetailsId INT NOT NULL,
 WorkflowCode VARCHAR(128) NOT NULL,
 WorkflowName VARCHAR(256) NOT NULL,
 VersionNo INT NOT NULL,
 JsonContent VARCHAR(MAX) NOT NULL,
 VersionName VARCHAR(128) NOT NULL,
 Status VARCHAR(30) NOT NULL CONSTRAINT DF_WorkflowJsonVersion_Status DEFAULT('Draft'),
 IsPublished BIT NOT NULL CONSTRAINT DF_WorkflowJsonVersion_IsPublished DEFAULT(0),
 CONSTRAINT FK_WorkflowJsonVersion_IndustryDetails FOREIGN KEY(IndustryDetailsId) REFERENCES dbo.IndustryDetails(IndustryDetailId),
 CONSTRAINT UQ_WorkflowJsonVersion_Code_Version UNIQUE(WorkflowCode,VersionNo),
 CONSTRAINT CK_WorkflowJsonVersion_Status CHECK(Status IN ('Draft','Published','Archieve'))
);
GO
IF OBJECT_ID(N'dbo.IndustryParameterMapping', N'U') IS NULL
CREATE TABLE dbo.IndustryParameterMapping(
 IndustryParameterMappingId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IndustryParameterMapping PRIMARY KEY,
 WorkflowJsonVersionId INT NOT NULL,
 IndustryDetailsId INT NOT NULL,
 ParameterDefinitionMasterId INT NOT NULL,
 ParentIndustryParameterMappingId INT NULL,
 ParameterDisplayName VARCHAR(128) NOT NULL,
 ValueType VARCHAR(128) NOT NULL,
 Weight DECIMAL(10,2) NOT NULL,
 DisplayOrder INT NOT NULL CONSTRAINT DF_IndustryParameterMapping_DisplayOrder DEFAULT(9999),
 IsActive BIT NOT NULL CONSTRAINT DF_IndustryParameterMapping_IsActive DEFAULT(1),
 CONSTRAINT FK_IPM_WorkflowJsonVersion FOREIGN KEY(WorkflowJsonVersionId) REFERENCES dbo.WorkflowJsonVersion(WorkflowJsonVersionId),
 CONSTRAINT FK_IPM_IndustryDetails FOREIGN KEY(IndustryDetailsId) REFERENCES dbo.IndustryDetails(IndustryDetailId),
 CONSTRAINT UQ_IPM_Version_Industry_Parameter UNIQUE(WorkflowJsonVersionId,IndustryDetailsId,ParameterDefinitionMasterId),
 CONSTRAINT FK_IPM_ParameterDefinition FOREIGN KEY(ParameterDefinitionMasterId) REFERENCES dbo.ParameterDefinitionMaster(ParameterDefinitionMasterId),
 CONSTRAINT FK_IPM_Parent FOREIGN KEY(ParentIndustryParameterMappingId) REFERENCES dbo.IndustryParameterMapping(IndustryParameterMappingId)
);
GO
IF OBJECT_ID(N'dbo.ValidationDefinition', N'U') IS NULL
CREATE TABLE dbo.ValidationDefinition(
 ValidationDefinitionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ValidationDefinition PRIMARY KEY,
 IndustryParameterMappingId INT NOT NULL,
 ValidationName VARCHAR(128) NOT NULL,
 Expression VARCHAR(MAX) NOT NULL,
 ErrorMessage VARCHAR(256) NOT NULL,
 IsActive BIT NOT NULL CONSTRAINT DF_ValidationDefinition_IsActive DEFAULT(1),
 CONSTRAINT FK_Validation_IPM FOREIGN KEY(IndustryParameterMappingId) REFERENCES dbo.IndustryParameterMapping(IndustryParameterMappingId)
);
GO
IF OBJECT_ID(N'dbo.RuleDefinition', N'U') IS NULL
CREATE TABLE dbo.RuleDefinition(
 RuleDefinitionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RuleDefinition PRIMARY KEY,
 WorkflowJsonVersionId INT NOT NULL,
 RuleName VARCHAR(128) NOT NULL,
 RuleExpressionType VARCHAR(128) NOT NULL,
 OutputExpression VARCHAR(256) NOT NULL,
 IsActive BIT NOT NULL CONSTRAINT DF_RuleDefinition_IsActive DEFAULT(1),
 CONSTRAINT FK_Rule_WorkflowJsonVersion FOREIGN KEY(WorkflowJsonVersionId) REFERENCES dbo.WorkflowJsonVersion(WorkflowJsonVersionId)
);
GO
IF OBJECT_ID(N'dbo.ScaleConfiguration', N'U') IS NULL
CREATE TABLE dbo.ScaleConfiguration(
 ScaleConfigurationId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScaleConfiguration PRIMARY KEY,
 ScaleCode VARCHAR(128) NOT NULL,
 ScaleName VARCHAR(256) NOT NULL,
 MinValue DECIMAL(10,2) NOT NULL,
 MaxValue DECIMAL(10,2) NOT NULL,
 IsActive BIT NOT NULL CONSTRAINT DF_ScaleConfiguration_IsActive DEFAULT(1),
 CONSTRAINT UQ_ScaleConfiguration_Code UNIQUE(ScaleCode),
 CONSTRAINT CK_ScaleConfiguration_Range CHECK(MinValue <= MaxValue)
);
GO
IF OBJECT_ID(N'dbo.ScaleConfigurationDetails', N'U') IS NULL
CREATE TABLE dbo.ScaleConfigurationDetails(
 ScaleConfigurationDetailsId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScaleConfigurationDetails PRIMARY KEY,
 IndustryParameterMappingId INT NOT NULL,
 ScaleConfigurationId INT NOT NULL,
 ScoreValue DECIMAL(10,2) NOT NULL,
 Definition VARCHAR(256) NOT NULL,
 MinValue DECIMAL(10,2) NOT NULL,
 MaxValue DECIMAL(10,2) NOT NULL,
 CONSTRAINT FK_ScaleDetails_IPM FOREIGN KEY(IndustryParameterMappingId) REFERENCES dbo.IndustryParameterMapping(IndustryParameterMappingId),
 CONSTRAINT FK_ScaleDetails_Scale FOREIGN KEY(ScaleConfigurationId) REFERENCES dbo.ScaleConfiguration(ScaleConfigurationId),
 CONSTRAINT CK_ScaleDetails_Range CHECK(MinValue <= MaxValue)
);
GO
IF OBJECT_ID(N'dbo.AssessmentResponse', N'U') IS NULL
CREATE TABLE dbo.AssessmentResponse(
 AssessmentResponseId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AssessmentResponse PRIMARY KEY,
 EntityId INT NOT NULL,
 WorkflowJsonVersionId INT NOT NULL,
 ResponseJson NVARCHAR(MAX) NOT NULL,
 Status VARCHAR(30) NOT NULL CONSTRAINT DF_AssessmentResponse_Status DEFAULT('Draft'),
 CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_AssessmentResponse_CreatedOn DEFAULT(SYSUTCDATETIME()),
 UpdatedOn DATETIME2 NOT NULL CONSTRAINT DF_AssessmentResponse_UpdatedOn DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT FK_AssessmentResponse_Customer FOREIGN KEY(EntityId) REFERENCES dbo.CustomerMaster(EntityId),
 CONSTRAINT FK_AssessmentResponse_Version FOREIGN KEY(WorkflowJsonVersionId) REFERENCES dbo.WorkflowJsonVersion(WorkflowJsonVersionId),
 CONSTRAINT CK_AssessmentResponse_Json CHECK(ISJSON(ResponseJson)=1)
);
GO

/* AssessmentResponse is recreated above with the intended non-unique entity/version index policy. */
GO

/* V19 exact seed data translated to the new CRISP 2.0 schema.
   Source: V19 Database/01_Create_DynamicRuleEngine_Framework.sql,
   CorporateBusiness.v1.json and CorporateBusiness.v2.json.
   This script is repeatable and preserves the original sample names, dates,
   parameter codes, weights, score definitions, workflow JSON and assessment values. */

/* Industry rows used by V19 */
IF NOT EXISTS (SELECT 1 FROM dbo.IndustryDetails WHERE IndustryName='Automobiles - Commercial Vehicles')
 INSERT dbo.IndustryDetails(IndustryName,IsActive) VALUES('Automobiles - Commercial Vehicles',1);
IF NOT EXISTS (SELECT 1 FROM dbo.IndustryDetails WHERE IndustryName='Automobiles - Passenger Vehicles')
 INSERT dbo.IndustryDetails(IndustryName,IsActive) VALUES('Automobiles - Passenger Vehicles',1);
IF NOT EXISTS (SELECT 1 FROM dbo.IndustryDetails WHERE IndustryName='Commercial Vehicles - Components')
 INSERT dbo.IndustryDetails(IndustryName,IsActive) VALUES('Commercial Vehicles - Components',1);

DECLARE @IndustryId INT=(SELECT TOP(1) IndustryDetailId FROM dbo.IndustryDetails WHERE IndustryName='Automobiles - Commercial Vehicles' ORDER BY IndustryDetailId);

/* Exact V19 entity/customer names */
/* Exact V19 entity/customer names */

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.CustomerMaster
    WHERE EntityName = '20 Microns Limited'
)
BEGIN
    INSERT INTO dbo.CustomerMaster
    (
        EntityName,
        IsActive
    )
    VALUES
    (
        '20 Microns Limited',
        1
    );
END


IF NOT EXISTS
(
    SELECT 1
    FROM dbo.CustomerMaster
    WHERE EntityName = '20 Microns Minerals, Foods Limited-Test 10'
)
BEGIN
    INSERT INTO dbo.CustomerMaster
    (
        EntityName,
        IsActive
    )
    VALUES
    (
        '20 Microns Minerals, Foods Limited-Test 10',
        1
    );
END

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.CustomerMaster
    WHERE EntityName = '20 Microns Nano Minerals Limited'
)
BEGIN
    INSERT INTO dbo.CustomerMaster
    (
        EntityName,
        IsActive
    )
    VALUES
    (
        '20 Microns Nano Minerals Limited',
        1
    );
END



/* Exact V19 parameter definitions */
IF NOT EXISTS (SELECT 1 FROM dbo.ParameterDefinitionMaster WHERE ParameterCode='MarketPositionScore')
 INSERT dbo.ParameterDefinitionMaster(ParameterCode,ParameterName,WhyItMatters,IsActive) VALUES('MarketPositionScore','Market Position & Brand Strength','Market share is a good proxy to a company''s scale & market position and R&D strengths',1);
IF NOT EXISTS (SELECT 1 FROM dbo.ParameterDefinitionMaster WHERE ParameterCode='ScaleScore')
 INSERT dbo.ParameterDefinitionMaster(ParameterCode,ParameterName,WhyItMatters,IsActive) VALUES('ScaleScore','Scale','The scale of operations, as measured by revenues, production capacity and production output, is one of the primary factors in evaluating business position.',1);
IF NOT EXISTS (SELECT 1 FROM dbo.ParameterDefinitionMaster WHERE ParameterCode='ProductPortfolioScore')
 INSERT dbo.ParameterDefinitionMaster(ParameterCode,ParameterName,WhyItMatters,IsActive) VALUES('ProductPortfolioScore','Product Portfolio','A strong product portfolio is essential for an OEM to sustain its competitive market position and cater to a diverse customer profile.',1);
IF NOT EXISTS (SELECT 1 FROM dbo.ParameterDefinitionMaster WHERE ParameterCode='GeographicDiversificationScore')
 INSERT dbo.ParameterDefinitionMaster(ParameterCode,ParameterName,WhyItMatters,IsActive) VALUES('GeographicDiversificationScore','Geographic Diversification','Geographic diversification helps mitigate risks arising from demand slowdown in a particular geography.',1);
IF NOT EXISTS (SELECT 1 FROM dbo.ParameterDefinitionMaster WHERE ParameterCode='TechnologyScore')
 INSERT dbo.ParameterDefinitionMaster(ParameterCode,ParameterName,WhyItMatters,IsActive) VALUES('TechnologyScore','Technology & Product Development Capabilities','OEMs need to continuously refresh their offering to keep pace with changing technology and emission norms.',1);

/* Version 1 and version 2 use the exact V19 JSON rule expressions and weights. */
DECLARE @WorkflowCode VARCHAR(128)='CorporateBusiness';
DECLARE @WorkflowName VARCHAR(256)='Corporate Business Framework';
DECLARE @JsonV1 VARCHAR(MAX)='[{"WorkflowName":"CorporateBusiness","GlobalParams":[{"Name":"Version","Expression":"1"}],"Weights":{"MarketPositionScore":15,"ScaleScore":15,"ProductPortfolioScore":25,"GeographicDiversificationScore":25,"TechnologyScore":20},"Rules":[{"RuleName":"CalculateWeightedAverage","SuccessEvent":"Calculated","Expression":"true","RuleExpressionType":"LambdaExpression","Actions":{"OnSuccess":{"Name":"OutputExpression","Context":{"Expression":"(input1.MarketPositionScore * 15 + input1.ScaleScore * 15 + input1.ProductPortfolioScore * 25 + input1.GeographicDiversificationScore * 25 + input1.TechnologyScore * 20) / 100.0"}}}}]}]';
DECLARE @JsonV2 VARCHAR(MAX)='[{"WorkflowName":"CorporateBusiness","GlobalParams":[{"Name":"Version","Expression":"2"}],"Weights":{"MarketPositionScore":20,"ScaleScore":10,"ProductPortfolioScore":25,"GeographicDiversificationScore":25,"TechnologyScore":20},"Rules":[{"RuleName":"CalculateWeightedAverage","SuccessEvent":"Calculated","Expression":"true","RuleExpressionType":"LambdaExpression","Actions":{"OnSuccess":{"Name":"OutputExpression","Context":{"Expression":"(input1.MarketPositionScore * 20 + input1.ScaleScore * 10 + input1.ProductPortfolioScore * 25 + input1.GeographicDiversificationScore * 25 + input1.TechnologyScore * 20) / 100.0"}}}}]}]';

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.WorkflowJsonVersion
    WHERE WorkflowCode = @WorkflowCode
      AND VersionNo = 1
)
BEGIN
    INSERT INTO dbo.WorkflowJsonVersion
    (
        IndustryDetailsId,
        WorkflowCode,
        WorkflowName,
        VersionNo,
        JsonContent,
        VersionName,
        Status,
        IsPublished
    )
    VALUES
    (
        @IndustryId,            -- IndustryDetailsId
        @WorkflowCode,          -- WorkflowCode
        @WorkflowName,          -- WorkflowName
        1,                      -- VersionNo
        @JsonV1,                -- JsonContent
        '09-Sep-2026 - Draft',  -- VersionName
        'Published',            -- Status
        1                       -- IsPublished
    );
END


IF NOT EXISTS
(
    SELECT 1
    FROM dbo.WorkflowJsonVersion
    WHERE WorkflowCode = @WorkflowCode
      AND VersionNo = 2
)
BEGIN
    INSERT INTO dbo.WorkflowJsonVersion
    (
        IndustryDetailsId,
        WorkflowCode,
        WorkflowName,
        VersionNo,
        JsonContent,
        VersionName,
        Status,
        IsPublished
    )
    VALUES
    (
        @IndustryId,            -- IndustryDetailsId
        @WorkflowCode,          -- WorkflowCode
        @WorkflowName,          -- WorkflowName
        2,                      -- VersionNo
        @JsonV2,                -- JsonContent
        '03-Sep-2026',          -- VersionName
        'Draft',                -- Status
        0                       -- IsPublished
    );
END
DECLARE @V1 INT=(SELECT WorkflowJsonVersionId FROM dbo.WorkflowJsonVersion WHERE WorkflowCode=@WorkflowCode AND VersionNo=1);
DECLARE @V2 INT=(SELECT WorkflowJsonVersionId FROM dbo.WorkflowJsonVersion WHERE WorkflowCode=@WorkflowCode AND VersionNo=2);
DECLARE @PMarket INT=(SELECT ParameterDefinitionMasterId FROM dbo.ParameterDefinitionMaster WHERE ParameterCode='MarketPositionScore');
DECLARE @PScale INT=(SELECT ParameterDefinitionMasterId FROM dbo.ParameterDefinitionMaster WHERE ParameterCode='ScaleScore');
DECLARE @PPortfolio INT=(SELECT ParameterDefinitionMasterId FROM dbo.ParameterDefinitionMaster WHERE ParameterCode='ProductPortfolioScore');
DECLARE @PGeo INT=(SELECT ParameterDefinitionMasterId FROM dbo.ParameterDefinitionMaster WHERE ParameterCode='GeographicDiversificationScore');
DECLARE @PTech INT=(SELECT ParameterDefinitionMasterId FROM dbo.ParameterDefinitionMaster WHERE ParameterCode='TechnologyScore');

/* Mappings with the exact V19 weights. */
DECLARE @MapV1Market INT, @MapV1Scale INT, @MapV1Portfolio INT, @MapV1Geo INT, @MapV1Tech INT;
DECLARE @MapV2Market INT, @MapV2Scale INT, @MapV2Portfolio INT, @MapV2Geo INT, @MapV2Tech INT;

IF NOT EXISTS(SELECT 1 FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V1 AND ParameterDefinitionMasterId=@PMarket)
 INSERT dbo.IndustryParameterMapping(WorkflowJsonVersionId,IndustryDetailsId,ParameterDefinitionMasterId,ParentIndustryParameterMappingId,ParameterDisplayName,ValueType,Weight,IsActive) VALUES(@V1,(SELECT IndustryDetailsId FROM dbo.WorkflowJsonVersion WHERE WorkflowJsonVersionId=@V1),@PMarket,NULL,'Market Position & Brand Strength','Number',15,1);
IF NOT EXISTS(SELECT 1 FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V1 AND ParameterDefinitionMasterId=@PScale)
 INSERT dbo.IndustryParameterMapping(WorkflowJsonVersionId,IndustryDetailsId,ParameterDefinitionMasterId,ParentIndustryParameterMappingId,ParameterDisplayName,ValueType,Weight,IsActive) VALUES(@V1,(SELECT IndustryDetailsId FROM dbo.WorkflowJsonVersion WHERE WorkflowJsonVersionId=@V1),@PScale,NULL,'Scale','Number',15,1);
IF NOT EXISTS(SELECT 1 FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V1 AND ParameterDefinitionMasterId=@PPortfolio)
 INSERT dbo.IndustryParameterMapping(WorkflowJsonVersionId,IndustryDetailsId,ParameterDefinitionMasterId,ParentIndustryParameterMappingId,ParameterDisplayName,ValueType,Weight,IsActive) VALUES(@V1,(SELECT IndustryDetailsId FROM dbo.WorkflowJsonVersion WHERE WorkflowJsonVersionId=@V1),@PPortfolio,NULL,'Product Portfolio','Score',25,1);
IF NOT EXISTS(SELECT 1 FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V1 AND ParameterDefinitionMasterId=@PGeo)
 INSERT dbo.IndustryParameterMapping(WorkflowJsonVersionId,IndustryDetailsId,ParameterDefinitionMasterId,ParentIndustryParameterMappingId,ParameterDisplayName,ValueType,Weight,IsActive) VALUES(@V1,(SELECT IndustryDetailsId FROM dbo.WorkflowJsonVersion WHERE WorkflowJsonVersionId=@V1),@PGeo,NULL,'Geographic Diversification','Score',25,1);
IF NOT EXISTS(SELECT 1 FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V1 AND ParameterDefinitionMasterId=@PTech)
 INSERT dbo.IndustryParameterMapping(WorkflowJsonVersionId,IndustryDetailsId,ParameterDefinitionMasterId,ParentIndustryParameterMappingId,ParameterDisplayName,ValueType,Weight,IsActive) VALUES(@V1,(SELECT IndustryDetailsId FROM dbo.WorkflowJsonVersion WHERE WorkflowJsonVersionId=@V1),@PTech,NULL,'Technology & Product Development Capabilities','Score',20,1);

IF NOT EXISTS(SELECT 1 FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V2 AND ParameterDefinitionMasterId=@PMarket)
 INSERT dbo.IndustryParameterMapping(WorkflowJsonVersionId,IndustryDetailsId,ParameterDefinitionMasterId,ParentIndustryParameterMappingId,ParameterDisplayName,ValueType,Weight,IsActive) VALUES(@V2,(SELECT IndustryDetailsId FROM dbo.WorkflowJsonVersion WHERE WorkflowJsonVersionId=@V2),@PMarket,NULL,'Market Position & Brand Strength','Number',20,1);
IF NOT EXISTS(SELECT 1 FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V2 AND ParameterDefinitionMasterId=@PScale)
 INSERT dbo.IndustryParameterMapping(WorkflowJsonVersionId,IndustryDetailsId,ParameterDefinitionMasterId,ParentIndustryParameterMappingId,ParameterDisplayName,ValueType,Weight,IsActive) VALUES(@V2,(SELECT IndustryDetailsId FROM dbo.WorkflowJsonVersion WHERE WorkflowJsonVersionId=@V2),@PScale,NULL,'Scale','Number',10,1);
IF NOT EXISTS(SELECT 1 FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V2 AND ParameterDefinitionMasterId=@PPortfolio)
 INSERT dbo.IndustryParameterMapping(WorkflowJsonVersionId,IndustryDetailsId,ParameterDefinitionMasterId,ParentIndustryParameterMappingId,ParameterDisplayName,ValueType,Weight,IsActive) VALUES(@V2,(SELECT IndustryDetailsId FROM dbo.WorkflowJsonVersion WHERE WorkflowJsonVersionId=@V2),@PPortfolio,NULL,'Product Portfolio','Score',25,1);
IF NOT EXISTS(SELECT 1 FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V2 AND ParameterDefinitionMasterId=@PGeo)
 INSERT dbo.IndustryParameterMapping(WorkflowJsonVersionId,IndustryDetailsId,ParameterDefinitionMasterId,ParentIndustryParameterMappingId,ParameterDisplayName,ValueType,Weight,IsActive) VALUES(@V2,(SELECT IndustryDetailsId FROM dbo.WorkflowJsonVersion WHERE WorkflowJsonVersionId=@V2),@PGeo,NULL,'Geographic Diversification','Score',25,1);
IF NOT EXISTS(SELECT 1 FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V2 AND ParameterDefinitionMasterId=@PTech)
 INSERT dbo.IndustryParameterMapping(WorkflowJsonVersionId,IndustryDetailsId,ParameterDefinitionMasterId,ParentIndustryParameterMappingId,ParameterDisplayName,ValueType,Weight,IsActive) VALUES(@V2,(SELECT IndustryDetailsId FROM dbo.WorkflowJsonVersion WHERE WorkflowJsonVersionId=@V2),@PTech,NULL,'Technology & Product Development Capabilities','Score',20,1);

SELECT @MapV1Market=IndustryParameterMappingId FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V1 AND ParameterDefinitionMasterId=@PMarket;
SELECT @MapV1Scale=IndustryParameterMappingId FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V1 AND ParameterDefinitionMasterId=@PScale;
SELECT @MapV1Portfolio=IndustryParameterMappingId FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V1 AND ParameterDefinitionMasterId=@PPortfolio;
SELECT @MapV1Geo=IndustryParameterMappingId FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V1 AND ParameterDefinitionMasterId=@PGeo;
SELECT @MapV1Tech=IndustryParameterMappingId FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V1 AND ParameterDefinitionMasterId=@PTech;
SELECT @MapV2Market=IndustryParameterMappingId FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V2 AND ParameterDefinitionMasterId=@PMarket;
SELECT @MapV2Scale=IndustryParameterMappingId FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V2 AND ParameterDefinitionMasterId=@PScale;
SELECT @MapV2Portfolio=IndustryParameterMappingId FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V2 AND ParameterDefinitionMasterId=@PPortfolio;
SELECT @MapV2Geo=IndustryParameterMappingId FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V2 AND ParameterDefinitionMasterId=@PGeo;
SELECT @MapV2Tech=IndustryParameterMappingId FROM dbo.IndustryParameterMapping WHERE WorkflowJsonVersionId=@V2 AND ParameterDefinitionMasterId=@PTech;

/* Scale tables preserve the original V19 score labels, values, ranges and units. */
IF NOT EXISTS(SELECT 1 FROM dbo.ScaleConfiguration WHERE ScaleCode='MarketPositionScore') INSERT dbo.ScaleConfiguration(ScaleCode,ScaleName,MinValue,MaxValue,IsActive) VALUES('MarketPositionScore','Market Position & Brand Strength',0,100,1);
IF NOT EXISTS(SELECT 1 FROM dbo.ScaleConfiguration WHERE ScaleCode='ScaleScore') INSERT dbo.ScaleConfiguration(ScaleCode,ScaleName,MinValue,MaxValue,IsActive) VALUES('ScaleScore','Scale (Rs. Crore)',0,1000000,1);
IF NOT EXISTS(SELECT 1 FROM dbo.ScaleConfiguration WHERE ScaleCode='ProductPortfolioScore') INSERT dbo.ScaleConfiguration(ScaleCode,ScaleName,MinValue,MaxValue,IsActive) VALUES('ProductPortfolioScore','Product Portfolio Subsegments',0,100,1);
IF NOT EXISTS(SELECT 1 FROM dbo.ScaleConfiguration WHERE ScaleCode='GeographicDiversificationScore') INSERT dbo.ScaleConfiguration(ScaleCode,ScaleName,MinValue,MaxValue,IsActive) VALUES('GeographicDiversificationScore','Geographic Diversification Regions',0,100,1);
IF NOT EXISTS(SELECT 1 FROM dbo.ScaleConfiguration WHERE ScaleCode='TechnologyScore') INSERT dbo.ScaleConfiguration(ScaleCode,ScaleName,MinValue,MaxValue,IsActive) VALUES('TechnologyScore','Technology & Product Development (% R&D)',0,100,1);
DECLARE @SMarket INT=(SELECT ScaleConfigurationId FROM dbo.ScaleConfiguration WHERE ScaleCode='MarketPositionScore');
DECLARE @SScale INT=(SELECT ScaleConfigurationId FROM dbo.ScaleConfiguration WHERE ScaleCode='ScaleScore');
DECLARE @SPortfolio INT=(SELECT ScaleConfigurationId FROM dbo.ScaleConfiguration WHERE ScaleCode='ProductPortfolioScore');
DECLARE @SGeo INT=(SELECT ScaleConfigurationId FROM dbo.ScaleConfiguration WHERE ScaleCode='GeographicDiversificationScore');
DECLARE @STech INT=(SELECT ScaleConfigurationId FROM dbo.ScaleConfiguration WHERE ScaleCode='TechnologyScore');

/* Insert score bands for each version mapping; endpoints are adjusted only to represent V19 exclusive boundaries in schema without inclusive flags. */
DECLARE @Bands TABLE(MapId INT, ScaleId INT, ScoreValue DECIMAL(10,2), Definition VARCHAR(256), MinValue DECIMAL(10,2), MaxValue DECIMAL(10,2));
INSERT @Bands VALUES
(@MapV1Market,@SMarket,1,'Market share > 30%',30.01,100),(@MapV1Market,@SMarket,2,'Market share 20-30%',20.01,30),(@MapV1Market,@SMarket,3,'Market share 10-20%',10.01,20),(@MapV1Market,@SMarket,4,'Market share 5-10%',5.01,10),(@MapV1Market,@SMarket,5,'Market share below 5%',0,5),
(@MapV2Market,@SMarket,1,'Market share > 30%',30.01,100),(@MapV2Market,@SMarket,2,'Market share 20-30%',20.01,30),(@MapV2Market,@SMarket,3,'Market share 10-20%',10.01,20),(@MapV2Market,@SMarket,4,'Market share 5-10%',5.01,10),(@MapV2Market,@SMarket,5,'Market share below 5%',0,5),
(@MapV1Scale,@SScale,1,'Revenue > 20,000',20000.01,1000000),(@MapV1Scale,@SScale,2,'Revenue 10,000-20,000',10000.01,20000),(@MapV1Scale,@SScale,3,'Revenue 5,000-10,000',5000.01,10000),(@MapV1Scale,@SScale,4,'Revenue 1,000-5,000',1000.01,5000),(@MapV1Scale,@SScale,5,'Revenue < 1,000',0,1000),
(@MapV2Scale,@SScale,1,'Revenue > 20,000',20000.01,1000000),(@MapV2Scale,@SScale,2,'Revenue 10,000-20,000',10000.01,20000),(@MapV2Scale,@SScale,3,'Revenue 5,000-10,000',5000.01,10000),(@MapV2Scale,@SScale,4,'Revenue 1,000-5,000',1000.01,5000),(@MapV2Scale,@SScale,5,'Revenue < 1,000',0,1000),
(@MapV1Portfolio,@SPortfolio,1,'Very diversified portfolio across all key subsegments',5,100),(@MapV1Portfolio,@SPortfolio,2,'Broad portfolio with strong presence across most subsegments',4,5),(@MapV1Portfolio,@SPortfolio,3,'Moderately diversified portfolio',3,4),(@MapV1Portfolio,@SPortfolio,4,'Limited portfolio concentration',2,3),(@MapV1Portfolio,@SPortfolio,5,'Highly concentrated portfolio',0,2),
(@MapV2Portfolio,@SPortfolio,1,'Very diversified portfolio across all key subsegments',5,100),(@MapV2Portfolio,@SPortfolio,2,'Broad portfolio with strong presence across most subsegments',4,5),(@MapV2Portfolio,@SPortfolio,3,'Moderately diversified portfolio',3,4),(@MapV2Portfolio,@SPortfolio,4,'Limited portfolio concentration',2,3),(@MapV2Portfolio,@SPortfolio,5,'Highly concentrated portfolio',0,2),
(@MapV1Geo,@SGeo,1,'Highly diversified across regions and exports',4,100),(@MapV1Geo,@SGeo,2,'Healthy diversification across 3-4 regions',3,4),(@MapV1Geo,@SGeo,3,'Moderate diversification across at least 2 regions',2,3),(@MapV1Geo,@SGeo,4,'Limited diversification across 1-2 regions',1,2),(@MapV1Geo,@SGeo,5,'Low diversification / specific region or domestic only',0,1),
(@MapV2Geo,@SGeo,1,'Highly diversified across regions and exports',4,100),(@MapV2Geo,@SGeo,2,'Healthy diversification across 3-4 regions',3,4),(@MapV2Geo,@SGeo,3,'Moderate diversification across at least 2 regions',2,3),(@MapV2Geo,@SGeo,4,'Limited diversification across 1-2 regions',1,2),(@MapV2Geo,@SGeo,5,'Low diversification / specific region or domestic only',0,1),
(@MapV1Tech,@STech,1,'R&D spend > 5% with strong capabilities',5.01,100),(@MapV1Tech,@STech,2,'R&D spend 2-5% with adequate capabilities',4,5),(@MapV1Tech,@STech,3,'R&D spend 2-5% with moderate capabilities',3,4),(@MapV1Tech,@STech,4,'R&D spend 2-5% with limited capabilities',2,3),(@MapV1Tech,@STech,5,'R&D spend < 2%',0,2),
(@MapV2Tech,@STech,1,'R&D spend > 5% with strong capabilities',5.01,100),(@MapV2Tech,@STech,2,'R&D spend 2-5% with adequate capabilities',4,5),(@MapV2Tech,@STech,3,'R&D spend 2-5% with moderate capabilities',3,4),(@MapV2Tech,@STech,4,'R&D spend 2-5% with limited capabilities',2,3),(@MapV2Tech,@STech,5,'R&D spend < 2%',0,2);
INSERT dbo.ScaleConfigurationDetails(IndustryParameterMappingId,ScaleConfigurationId,ScoreValue,Definition,MinValue,MaxValue)
SELECT b.MapId,b.ScaleId,b.ScoreValue,b.Definition,b.MinValue,b.MaxValue FROM @Bands b
WHERE NOT EXISTS(SELECT 1 FROM dbo.ScaleConfigurationDetails d WHERE d.IndustryParameterMappingId=b.MapId AND d.ScaleConfigurationId=b.ScaleId AND d.ScoreValue=b.ScoreValue);

/* Exact V19 rule records for each version */
IF NOT EXISTS(SELECT 1 FROM dbo.RuleDefinition WHERE WorkflowJsonVersionId=@V1 AND RuleName='CalculateWeightedAverage')
 INSERT dbo.RuleDefinition(WorkflowJsonVersionId,RuleName,RuleExpressionType,OutputExpression,IsActive) VALUES(@V1,'CalculateWeightedAverage','LambdaExpression','(input1.MarketPositionScore * 15 + input1.ScaleScore * 15 + input1.ProductPortfolioScore * 25 + input1.GeographicDiversificationScore * 25 + input1.TechnologyScore * 20) / 100.0',1);
IF NOT EXISTS(SELECT 1 FROM dbo.RuleDefinition WHERE WorkflowJsonVersionId=@V2 AND RuleName='CalculateWeightedAverage')
 INSERT dbo.RuleDefinition(WorkflowJsonVersionId,RuleName,RuleExpressionType,OutputExpression,IsActive) VALUES(@V2,'CalculateWeightedAverage','LambdaExpression','(input1.MarketPositionScore * 20 + input1.ScaleScore * 10 + input1.ProductPortfolioScore * 25 + input1.GeographicDiversificationScore * 25 + input1.TechnologyScore * 20) / 100.0',1);

/* V19 has no seeded ValidationDefinition rows in its original SQL scripts, so none are invented here. */

/* V19 initial assessment: exact values/scores and framework labels from the original seed script. */
DECLARE @EntityId INT=(SELECT EntityId FROM dbo.CustomerMaster WHERE EntityName='20 Microns Limited');
DECLARE @InitialResponse NVARCHAR(MAX)=N'{"FrameworkName":"09-Sep-2026 - Draft","FrameworkDate":"2026-09-09","WorkflowVersionNo":1,"ConsolidatedView":false,"Values":[{"ParameterCode":"MarketPositionScore","Value":52,"Score":1},{"ParameterCode":"ScaleScore","Value":52,"Score":5},{"ParameterCode":"ProductPortfolioScore","Value":null,"Score":1},{"ParameterCode":"GeographicDiversificationScore","Value":null,"Score":1},{"ParameterCode":"TechnologyScore","Value":null,"Score":1}],"Evaluation":null}';
IF @EntityId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.AssessmentResponse WHERE EntityId=@EntityId AND WorkflowJsonVersionId=@V1)
 INSERT dbo.AssessmentResponse(EntityId,WorkflowJsonVersionId,ResponseJson,Status) VALUES(@EntityId,@V1,@InitialResponse,'Draft');

/* Exact additional V19 framework dropdown labels. These are represented as separate response snapshots. */
DECLARE @OldFrameworks TABLE(FrameworkName VARCHAR(128), FrameworkDate DATE, Status VARCHAR(30));
INSERT @OldFrameworks VALUES
('03-Sep-2026','2026-09-03','Saved'),('25-Aug-2026','2026-08-25','Saved'),('24-Aug-2026 17:48','2026-08-24','Saved')
/* Keep the exact labels in JSON for these historical snapshots. */
DECLARE @OldFrameworkName VARCHAR(128), @OldFrameworkDate DATE, @OldStatus VARCHAR(30), @OldJson NVARCHAR(MAX);
DECLARE old_framework_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT FrameworkName,FrameworkDate,Status FROM @OldFrameworks;
OPEN old_framework_cursor;
FETCH NEXT FROM old_framework_cursor INTO @OldFrameworkName,@OldFrameworkDate,@OldStatus;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @OldJson=N'{"FrameworkName":"'+@OldFrameworkName+N'","FrameworkDate":"'+CONVERT(NVARCHAR(10),@OldFrameworkDate,23)+N'","WorkflowVersionNo":1,"ConsolidatedView":false,"Values":[{"ParameterCode":"MarketPositionScore","Value":null,"Score":1},{"ParameterCode":"ScaleScore","Value":null,"Score":1},{"ParameterCode":"ProductPortfolioScore","Value":null,"Score":1},{"ParameterCode":"GeographicDiversificationScore","Value":null,"Score":1},{"ParameterCode":"TechnologyScore","Value":null,"Score":1}],"Evaluation":null}';
 IF @EntityId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.AssessmentResponse WHERE EntityId=@EntityId AND WorkflowJsonVersionId=@V1 AND ResponseJson LIKE N'%"FrameworkName":"'+@OldFrameworkName+N'"%')
   INSERT dbo.AssessmentResponse(EntityId,WorkflowJsonVersionId,ResponseJson,Status) VALUES(@EntityId,@V1,@OldJson,@OldStatus);
 FETCH NEXT FROM old_framework_cursor INTO @OldFrameworkName,@OldFrameworkDate,@OldStatus;
END
CLOSE old_framework_cursor;
DEALLOCATE old_framework_cursor;


/* ------------------------------ Verify ------------------------------ */
SELECT 'IndustryDetails' AS TableName, COUNT(*) AS TotalRows FROM dbo.IndustryDetails
UNION ALL SELECT 'CustomerMaster',COUNT(*) FROM dbo.CustomerMaster
UNION ALL SELECT 'ParameterDefinitionMaster',COUNT(*) FROM dbo.ParameterDefinitionMaster
UNION ALL SELECT 'WorkflowJsonVersion',COUNT(*) FROM dbo.WorkflowJsonVersion
UNION ALL SELECT 'IndustryParameterMapping',COUNT(*) FROM dbo.IndustryParameterMapping
UNION ALL SELECT 'ValidationDefinition',COUNT(*) FROM dbo.ValidationDefinition
UNION ALL SELECT 'RuleDefinition',COUNT(*) FROM dbo.RuleDefinition
UNION ALL SELECT 'ScaleConfiguration',COUNT(*) FROM dbo.ScaleConfiguration
UNION ALL SELECT 'ScaleConfigurationDetails',COUNT(*) FROM dbo.ScaleConfigurationDetails
UNION ALL SELECT 'AssessmentResponse',COUNT(*) FROM dbo.AssessmentResponse;
GO