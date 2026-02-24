namespace Maliev.ContactService.Api.Exceptions;

/// <summary>
/// Exception thrown when the Country Service is unavailable or fails to respond.
/// </summary>
public class CountryServiceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CountryServiceException"/> class with a default message.
    /// </summary>
    public CountryServiceException()
        : base("Unable to validate country information. Please try again in a few moments.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CountryServiceException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CountryServiceException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CountryServiceException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public CountryServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
