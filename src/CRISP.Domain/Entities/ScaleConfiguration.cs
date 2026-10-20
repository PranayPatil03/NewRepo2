namespace CRISP.Domain.Entities;
public sealed class ScaleConfiguration  {
    public int ScaleConfigurationId  {
        get;
        set;
    }
    public string ScaleCode  {
        get;
        set;
    }
    = "";
    public string ScaleName  {
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
    public bool IsActive  {
        get;
        set;
    }
    = true;
}
