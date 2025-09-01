namespace Maliev.MessageService.Api.Models.DTOs
{
    using System;

    public class MessageDto
    {
        public int Id { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Company { get; set; }
        public required string Email { get; set; }
        public required string Telephone { get; set; }
        public required string Country { get; set; }
        public required string MessageContent { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}