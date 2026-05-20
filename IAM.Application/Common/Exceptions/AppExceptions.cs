namespace IAM.Application.Common.Exceptions;

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

// by base constructor, the message is set to a generic one. The actual errors are in the Errors property.
// base se parent constructor called, message set to "One or more validation failures occurred."
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation failures occurred.")
        
    {
        Errors = errors;
    }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Unauthorized access.") : base(message) { }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public class NotFoundException : Exception
{
    public NotFoundException(string resource, object key)
        : base($"{resource} with key '{key}' was not found.") { }
}