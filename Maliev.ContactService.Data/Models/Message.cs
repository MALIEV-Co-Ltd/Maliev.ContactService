namespace Maliev.MessageService.Data.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a legacy message entity.
    /// </summary>
    public partial class Message
    {
        /// <summary>
        /// Gets or sets the unique identifier for the message.
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Gets or sets the first name of the message sender.
        /// </summary>
        public required string FirstName { get; set; }
        
        /// <summary>
        /// Gets or sets the last name of the message sender.
        /// </summary>
        public required string LastName { get; set; }
        
        /// <summary>
        /// Gets or sets the company name of the message sender.
        /// </summary>
        public required string Company { get; set; }
        
        /// <summary>
        /// Gets or sets the email address of the message sender.
        /// </summary>
        public required string Email { get; set; }
        
        /// <summary>
        /// Gets or sets the telephone number of the message sender.
        /// </summary>
        public required string Telephone { get; set; }
        
        /// <summary>
        /// Gets or sets the country of the message sender.
        /// </summary>
        public required string Country { get; set; }
        
        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public required string MessageContent { get; set; }
        
        /// <summary>
        /// Gets or sets the date and time when the message was created.
        /// </summary>
        public DateTime? CreatedDate { get; set; }
        
        /// <summary>
        /// Gets or sets the date and time when the message was last modified.
        /// </summary>
        public DateTime? ModifiedDate { get; set; }
    }
}
