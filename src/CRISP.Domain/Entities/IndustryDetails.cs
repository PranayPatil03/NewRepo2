namespace CRISP.Domain.Entities;

public sealed class IndustryDetails
{
    public int IndustryDetailId { get; set; }

    public string IndustryName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<WorkflowJsonVersion> WorkflowJsonVersions { get; set; }
        = new List<WorkflowJsonVersion>();
}