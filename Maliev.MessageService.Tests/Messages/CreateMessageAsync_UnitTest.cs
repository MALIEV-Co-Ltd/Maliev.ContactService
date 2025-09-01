// <copyright file="CreateMessageAsync_UnitTest.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Maliev.MessageService.Tests.Messages
{
    using System;
    using System.Threading.Tasks;
    using Maliev.MessageService.Api.Controllers;
    using Maliev.MessageService.Api.Models.DTOs;
    using Maliev.MessageService.Api.Services;
    using Microsoft.AspNetCore.Mvc;
    using Moq;
    using Xunit;

    /// <summary>
    /// UnitTest.
    /// </summary>
    public class CreateMessageAsync_UnitTest
    {
        private readonly Mock<IMessageService> _mockMessageService;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateMessageAsync_UnitTest"/> class.
        /// </summary>
        public CreateMessageAsync_UnitTest()
        {
            _mockMessageService = new Mock<IMessageService>();
        }

        /// <summary>
        /// Invalid message, should return bad request object result.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task InvalidMessage_ShouldReturnBadRequestObjectResult()
        {
            // Arrange
            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.CreateMessageAsync(null);

            // Assert
            Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            _mockMessageService.Verify(s => s.CreateMessageAsync(It.IsAny<CreateMessageRequest>()), Times.Never());
        }

        /// <summary>
        /// Valid message, should return created at route.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task ValidMessage_ShouldReturnCreatedAtRoute()
        {
            // Arrange
            var request = new CreateMessageRequest
            {
                FirstName = "Test first name",
                LastName = "Test last name",
                Company = "Test company",
                Email = "test@example.com",
                Telephone = "1234567890",
                Country = "Test country",
                MessageContent = "Test message content",
            };

            var messageDto = new MessageDto
            {
                Id = 1,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Company = request.Company,
                Email = request.Email,
                Telephone = request.Telephone,
                Country = request.Country,
                MessageContent = request.MessageContent,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };

            _mockMessageService.Setup(s => s.CreateMessageAsync(It.IsAny<CreateMessageRequest>()))
                .ReturnsAsync(messageDto);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.CreateMessageAsync(request);

            // Assert
            var createdAtRouteResult = Assert.IsType<CreatedAtRouteResult>(actionResult.Result);
            var returnedDto = Assert.IsType<MessageDto>(createdAtRouteResult.Value);
            Assert.Equal(messageDto.Id, returnedDto.Id);
            Assert.Equal(messageDto.FirstName, returnedDto.FirstName);
            _mockMessageService.Verify(s => s.CreateMessageAsync(It.IsAny<CreateMessageRequest>()), Times.Once);
        }
    }
}
