namespace CRISP.Domain.Entities;

public sealed class WorkflowJsonVersion
{
    public int WorkflowJsonVersionId { get; set; }

    public int IndustryDetailsId { get; set; }

    public string WorkflowCode { get; set; } = string.Empty;

    public string WorkflowName { get; set; } = string.Empty;

    public int VersionNo { get; set; }

    public string JsonContent { get; set; } = string.Empty;

    public string VersionName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    public IndustryDetails IndustryDetails { get; set; } = null!;
}