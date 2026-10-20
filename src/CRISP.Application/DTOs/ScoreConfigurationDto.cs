namespace CRISP.Application.DTOs;

public sealed class ScoreConfigurationDto
{
    public int IndustryParameterMappingId { get; set; }

    public int? ScaleConfigurationId { get; set; }

    public string ScaleCode { get; set; } = string.Empty;

    public string ScaleName { get; set; } = string.Empty;

    public decimal MinValue { get; set; }

    public decimal MaxValue { get; set; }

    public List<ScoreDto> Scores { get; set; } = [];
}