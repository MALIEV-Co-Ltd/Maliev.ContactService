namespace Maliev.ContactService.Data.Models;

/// <summary>
/// Interface for entities that track creation and update timestamps.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// Gets or sets the timestamp when the entity was created.
    /// </summary>
    DateTimeOffset CreatedAt { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp when the entity was last updated.
    /// </summary>
    DateTimeOffset UpdatedAt { get; set; }
}
