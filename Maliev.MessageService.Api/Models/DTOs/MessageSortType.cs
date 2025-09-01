namespace Maliev.MessageService.Api.Models.DTOs
{
    /// <summary>
    /// Sort Type.
    /// </summary>
    public enum MessageSortType
    {
        /// <summary>
        /// The message identifier ascending
        /// </summary>
        MessageId_Ascending,

        /// <summary>
        /// The message identifier descending
        /// </summary>
        MessageId_Descending,

        /// <summary>
        /// The message created date ascending
        /// </summary>
        MessageCreatedDate_Ascending,

        /// <summary>
        /// The message created date descending
        /// </summary>
        MessageCreatedDate_Descending,
    }
}