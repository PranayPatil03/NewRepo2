namespace CRISP.Domain.Entities;
public sealed class CustomerMaster  {
    public int EntityId  {
        get;
        set;
    }
    public string EntityName  {
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
