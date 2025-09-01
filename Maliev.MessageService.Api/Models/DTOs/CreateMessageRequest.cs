namespace Maliev.MessageService.Api.Models.DTOs
{
    using System.ComponentModel.DataAnnotations;

    public class CreateMessageRequest
    {
        [Required]
        [MaxLength(50)]
        public required string FirstName { get; set; }

        [Required]
        [MaxLength(50)]
        public required string LastName { get; set; }

        [MaxLength(50)]
        public string Company { get; set; }

        [Required]
        [MaxLength(50)]
        [EmailAddress]
        public required string Email { get; set; }

        [MaxLength(50)]
        public string Telephone { get; set; }

        [MaxLength(50)]
        public string Country { get; set; }

        [Required]
        public required string MessageContent { get; set; }
    }
}