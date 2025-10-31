namespace Maliev.ContactService.Api.Exceptions;

/// <summary>
/// Exception thrown when a duplicate inquiry is detected from the same email within 60 seconds.
/// </summary>
public class DuplicateInquiryException : Exception
{
    public DuplicateInquiryException()
        : base("You have recently submitted a contact form. Please wait before submitting again.")
    {
    }

    public DuplicateInquiryException(string message)
        : base(message)
    {
    }

    public DuplicateInquiryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
