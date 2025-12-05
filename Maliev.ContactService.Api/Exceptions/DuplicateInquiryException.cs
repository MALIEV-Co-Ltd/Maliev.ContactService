namespace Maliev.ContactService.Api.Exceptions;

/// <summary>
/// Exception thrown when a duplicate inquiry is detected from the same email within 60 seconds.
/// </summary>
public class DuplicateInquiryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateInquiryException"/> class with a default message.
    /// </summary>
    public DuplicateInquiryException()
        : base("You have recently submitted a contact form. Please wait before submitting again.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateInquiryException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DuplicateInquiryException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateInquiryException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public DuplicateInquiryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
