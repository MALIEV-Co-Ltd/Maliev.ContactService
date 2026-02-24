using Maliev.ContactService.Api.Exceptions;
using Xunit;

namespace Maliev.ContactService.Tests.Exceptions;

public class ExceptionTests
{
    [Fact]
    public void DuplicateInquiryException_DefaultConstructor_SetsMessage()
    {
        var ex = new DuplicateInquiryException();
        Assert.Equal("You have recently submitted a contact form. Please wait before submitting again.", ex.Message);
    }

    [Fact]
    public void DuplicateInquiryException_MessageConstructor_SetsMessage()
    {
        var message = "Custom duplicate message";
        var ex = new DuplicateInquiryException(message);
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void DuplicateInquiryException_MessageAndInnerExceptionConstructor_SetsProperties()
    {
        var message = "Custom duplicate message";
        var inner = new Exception("Inner");
        var ex = new DuplicateInquiryException(message, inner);
        Assert.Equal(message, ex.Message);
        Assert.Equal(inner, ex.InnerException);
    }

    [Fact]
    public void CountryServiceException_DefaultConstructor_SetsMessage()
    {
        var ex = new CountryServiceException();
        Assert.Equal("Unable to validate country information. Please try again in a few moments.", ex.Message);
    }

    [Fact]
    public void CountryServiceException_MessageConstructor_SetsMessage()
    {
        var message = "Custom country error";
        var ex = new CountryServiceException(message);
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void CountryServiceException_MessageAndInnerExceptionConstructor_SetsProperties()
    {
        var message = "Custom country error";
        var inner = new Exception("Inner");
        var ex = new CountryServiceException(message, inner);
        Assert.Equal(message, ex.Message);
        Assert.Equal(inner, ex.InnerException);
    }

    [Fact]
    public void NotFoundException_Constructor_SetsMessage()
    {
        var message = "Not found";
        var ex = new NotFoundException(message);
        Assert.Equal(message, ex.Message);
    }
}
