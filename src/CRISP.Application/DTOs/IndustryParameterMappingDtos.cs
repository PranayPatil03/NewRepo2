namespace CRISP.Application.DTOs;

/// <summary>One master parameter and its configuration for a specific workflow version.</summary>
public sealed class IndustryParameterMappingDto
{
    public int IndustryParameterMappingId { get; set; }
    public int ParameterDefinitionMasterId { get; set; }
    public string ParameterCode { get; set; } = string.Empty;
    public string MasterParameterName { get; set; } = string.Empty;
    public bool IsMapped { get; set; }
    public string ParameterDisplayName { get; set; } = string.Empty;
    public string ValueType { get; set; } = "Score";
    public decimal Weight { get; set; }
    public int? ScaleConfigurationId { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class SaveIndustryParameterMappingsRequest
{
    public string WorkflowCode { get; set; } = string.Empty;
    public int VersionNo { get; set; }
    public int IndustryDetailsId { get; set; }
    public List<IndustryParameterMappingDto> Mappings { get; set; } = [];
}
