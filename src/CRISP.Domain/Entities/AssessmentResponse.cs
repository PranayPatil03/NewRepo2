namespace CRISP.Domain.Entities;
/// <summary>Entity-specific answers for a selected immutable workflow version.</summary>
public sealed class AssessmentResponse  {
    public int AssessmentResponseId  {
        get;
        set;
    }
    public int EntityId  {
        get;
        set;
    }
    public int WorkflowJsonVersionId  {
        get;
        set;
    }
    public string ResponseJson  {
        get;
        set;
    }
    = "{}";
    public string Status  {
        get;
        set;
    }
    = "Draft";
    public DateTime CreatedOn  {
        get;
        set;
    }
    = DateTime.UtcNow;
    public DateTime UpdatedOn  {
        get;
        set;
    }
    = DateTime.UtcNow;
    public CustomerMaster CustomerMaster  {
        get;
        set;
    }
    = null!;
    public WorkflowJsonVersion WorkflowJsonVersion  {
        get;
        set;
    }
    = null!;
}
