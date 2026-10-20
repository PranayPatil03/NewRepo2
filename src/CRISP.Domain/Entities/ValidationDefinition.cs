namespace CRISP.Domain.Entities;
public sealed class ValidationDefinition  {
    public int ValidationDefinitionId  {
        get;
        set;
    }
    public int IndustryParameterMappingId  {
        get;
        set;
    }
    public string ValidationName  {
        get;
        set;
    }
    = "";
    public string Expression  {
        get;
        set;
    }
    = "true";
    public string ErrorMessage  {
        get;
        set;
    }
    = "Validation failed.";
    public bool IsActive  {
        get;
        set;
    }
    = true;
}
