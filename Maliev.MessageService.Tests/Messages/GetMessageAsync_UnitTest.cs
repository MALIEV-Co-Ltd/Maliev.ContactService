// <copyright file="GetMessageAsync_UnitTest.cs" company="Maliev Company Limited">
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
    public class GetMessageAsync_UnitTest
    {
        private readonly Mock<IMessageService> _mockMessageService;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetMessageAsync_UnitTest"/> class.
        /// </summary>
        public GetMessageAsync_UnitTest()
        {
            _mockMessageService = new Mock<IMessageService>();
        }

        /// <summary>
        /// Message exist, should return job message.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task MessageExist_ShouldReturnJobMessage()
        {
            // Arrange
            var messageDto = new MessageDto
            {
                Id = 1,
                FirstName = "Test first name",
                LastName = "Test last name",
                Company = "Test company",
                Email = "test@example.com",
                Telephone = "1234567890",
                Country = "Test country",
                MessageContent = "Test message content",
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };

            _mockMessageService.Setup(s => s.GetMessageAsync(It.IsAny<int>()))
                .ReturnsAsync(messageDto);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetMessageAsync(1);

            // Assert
            var okResult = Assert.IsType<ActionResult<MessageDto>>(actionResult);
            var returnedDto = Assert.IsType<MessageDto>(okResult.Value);
            Assert.Equal(messageDto.Id, returnedDto.Id);
            _mockMessageService.Verify(s => s.GetMessageAsync(1), Times.Once);
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
            _mockMessageService.Setup(s => s.GetMessageAsync(It.IsAny<int>()))
                .ReturnsAsync((MessageDto)null);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetMessageAsync(int.MaxValue);

            // Assert
            Assert.IsType<NotFoundResult>(actionResult.Result);
            _mockMessageService.Verify(s => s.GetMessageAsync(int.MaxValue), Times.Once);
        }
    }
}