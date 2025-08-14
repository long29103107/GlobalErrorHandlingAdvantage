namespace GlobalErrorHandlingAdvantage.Exceptions;

public sealed class ValidationException : DomainException
{
    public ValidationException(string message, IDictionary<string, string[]> errors) 
        : base(message)
    {
        Errors = errors;
    }
    public IDictionary<string, string[]> Errors { get; }
    public override int StatusCode => 400;
    public override string ErrorType => "validation_error";
}