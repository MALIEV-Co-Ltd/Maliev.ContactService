// <copyright file="UpdateMessageAsync_UnitTest.cs" company="Maliev Company Limited">
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
    public class UpdateMessageAsync_UnitTest
    {
        private readonly Mock<IMessageService> _mockMessageService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateMessageAsync_UnitTest"/> class.
        /// </summary>
        public UpdateMessageAsync_UnitTest()
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
            var actionResult = await controller.UpdateMessageAsync(int.MaxValue, null);

            // Assert
            Assert.IsType<BadRequestObjectResult>(actionResult);
            _mockMessageService.Verify(s => s.UpdateMessageAsync(It.IsAny<int>(), It.IsAny<UpdateMessageRequest>()), Times.Never());
        }

        /// <summary>
        /// Message not exist, should return not found.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task MessageNotExist_ShouldReturnNotFound()
        {
            // Arrange
            _mockMessageService.Setup(s => s.UpdateMessageAsync(It.IsAny<int>(), It.IsAny<UpdateMessageRequest>()))
                .ThrowsAsync(new Exception("Message not found"));

            var controller = new MessagesController(_mockMessageService.Object);
            var request = new UpdateMessageRequest
            {
                FirstName = "Test first name",
                LastName = "Test last name",
                Company = "Test company",
                Email = "test@example.com",
                Telephone = "1234567890",
                Country = "Test country",
                MessageContent = "Test message content",
            };

            // Act
            var actionResult = await controller.UpdateMessageAsync(int.MaxValue, request);

            // Assert
            Assert.IsType<NotFoundResult>(actionResult);
            _mockMessageService.Verify(s => s.UpdateMessageAsync(int.MaxValue, It.IsAny<UpdateMessageRequest>()), Times.Once);
        }

        /// <summary>
        /// Valid message, should return no content.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task ValidMessage_ShouldReturnNoContent()
        {
            // Arrange
            _mockMessageService.Setup(s => s.UpdateMessageAsync(It.IsAny<int>(), It.IsAny<UpdateMessageRequest>()))
                .Returns(Task.CompletedTask);

            var controller = new MessagesController(_mockMessageService.Object);
            var request = new UpdateMessageRequest
            {
                FirstName = "Test first name",
                LastName = "Test last name",
                Company = "Test company",
                Email = "test@example.com",
                Telephone = "1234567890",
                Country = "Test country",
                MessageContent = "Test message content",
            };

            // Act
            var actionResult = await controller.UpdateMessageAsync(1, request);

            // Assert
            Assert.IsType<NoContentResult>(actionResult);
            _mockMessageService.Verify(s => s.UpdateMessageAsync(1, It.IsAny<UpdateMessageRequest>()), Times.Once);
        }
    }
}
