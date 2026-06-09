namespace Web.Services;

public class ServerValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ServerValidationException(IDictionary<string, string[]> errors) 
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}
