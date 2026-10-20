namespace CRISP.Domain.Entities;
public sealed class RuleDefinition  {
    public int RuleDefinitionId  {
        get;
        set;
    }
    public int WorkflowJsonVersionId  {
        get;
        set;
    }
    public string RuleName  {
        get;
        set;
    }
    = "";
    public string RuleExpressionType  {
        get;
        set;
    }
    = "LambdaExpression";
    public string OutputExpression  {
        get;
        set;
    }
    = "0";
    public bool IsActive  {
        get;
        set;
    }
    = true;
}
