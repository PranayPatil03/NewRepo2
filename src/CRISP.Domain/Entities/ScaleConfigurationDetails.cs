namespace CRISP.Domain.Entities;
public sealed class ScaleConfigurationDetails  {
    public int ScaleConfigurationDetailsId  {
        get;
        set;
    }
    public int IndustryParameterMappingId  {
        get;
        set;
    }
    public int ScaleConfigurationId  {
        get;
        set;
    }
    public decimal ScoreValue  {
        get;
        set;
    }
    public string Definition  {
        get;
        set;
    }
    = "";
    public decimal MinValue  {
        get;
        set;
    }
    public decimal MaxValue  {
        get;
        set;
    }
}
