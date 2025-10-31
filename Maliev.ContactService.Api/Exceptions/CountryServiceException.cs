namespace Maliev.ContactService.Api.Exceptions;

/// <summary>
/// Exception thrown when the Country Service is unavailable or fails to respond.
/// </summary>
public class CountryServiceException : Exception
{
    public CountryServiceException()
        : base("Unable to validate country information. Please try again in a few moments.")
    {
    }

    public CountryServiceException(string message)
        : base(message)
    {
    }

    public CountryServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
