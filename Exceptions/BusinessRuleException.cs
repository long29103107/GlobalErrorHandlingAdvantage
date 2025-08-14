namespace GlobalErrorHandlingAdvantage.Exceptions;

public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message) { }
    public override int StatusCode => 422;
    public override string ErrorType => "business_rule_violation";
}