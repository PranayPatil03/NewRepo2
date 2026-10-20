namespace CRISP.Domain.Entities;
public sealed class ParameterDefinitionMaster  {
    public int ParameterDefinitionMasterId  {
        get;
        set;
    }
    public string ParameterCode  {
        get;
        set;
    }
    = "";
    public string ParameterName  {
        get;
        set;
    }
    = "";
    public string WhyItMatters  {
        get;
        set;
    }
    = "";
    public bool IsActive  {
        get;
        set;
    }
    = true;
}
