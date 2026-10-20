namespace CRISP.Web.Models;
public sealed record ApiVersionDetail
 {
    public int Id  {
        get;
        init;
    }
    public int VersionNo  {
        get;
        init;
    }
    public string VersionName  {
        get;
        init;
    }
    = string.Empty;
    public string Status  {
        get;
        init;
    }
    = string.Empty;
    public bool IsPublished  {
        get;
        init;
    }
    public DateTime CreatedOn  {
        get;
        init;
    }
    public string CreatedBy  {
        get;
        init;
    }
    = string.Empty;
    public string JsonContent  {
        get;
        init;
    }
    = string.Empty;
}
