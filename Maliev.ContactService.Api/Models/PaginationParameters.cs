using System.ComponentModel.DataAnnotations;

namespace Maliev.ContactService.Api.Models;
/// <summary>
/// Represents a PaginationParameters
/// </summary>

public class PaginationParameters
{
    /// <summary>
    /// Gets or sets the page number for pagination (1-based).
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0")]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100")]
    public int PageSize { get; set; } = 20;
}
