namespace CRISP.Domain.Entities;
public sealed class IndustryParameterMapping  {
    public int IndustryParameterMappingId  {
        get;
        set;
    }
    public int IndustryDetailsId { get; set; }
    public int WorkflowJsonVersionId  {
        get;
        set;
    }
    public int ParameterDefinitionMasterId  {
        get;
        set;
    }
    public int? ParentIndustryParameterMappingId  {
        get;
        set;
    }
    public string ParameterDisplayName  {
        get;
        set;
    }
    = "";
    public string ValueType  {
        get;
        set;
    }
    = "ManualScore";
    public int DisplayOrder { get; set; } = 9999;
    public decimal Weight  {
        get;
        set;
    }
    public bool IsActive  {
        get;
        set;
    }
    = true;
}
