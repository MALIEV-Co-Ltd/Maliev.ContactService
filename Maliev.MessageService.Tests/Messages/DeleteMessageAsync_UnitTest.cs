// <copyright file="DeleteMessageAsync_UnitTest.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Maliev.MessageService.Tests.Messages
{
    using System;
    using System.Threading.Tasks;
    using Maliev.MessageService.Api.Controllers;
    using Maliev.MessageService.Api.Services;
    using Microsoft.AspNetCore.Mvc;
    using Moq;
    using Xunit;

    /// <summary>
    /// UnitTest.
    /// </summary>
    public class DeleteMessageAsync_UnitTest
    {
        private readonly Mock<IMessageService> _mockMessageService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteMessageAsync_UnitTest"/> class.
        /// </summary>
        public DeleteMessageAsync_UnitTest()
        {
            _mockMessageService = new Mock<IMessageService>();
        }

        /// <summary>
        /// Message exist, should return no content.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task MessageExist_ShouldReturnNoContent()
        {
            // Arrange
            _mockMessageService.Setup(s => s.DeleteMessageAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.DeleteMessageAsync(1);

            // Assert
            Assert.IsType<NoContentResult>(actionResult);
            _mockMessageService.Verify(s => s.DeleteMessageAsync(1), Times.Once);
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
            _mockMessageService.Setup(s => s.DeleteMessageAsync(It.IsAny<int>()))
                .ThrowsAsync(new Exception("Message not found"));

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.DeleteMessageAsync(int.MaxValue);

            // Assert
            Assert.IsType<NotFoundResult>(actionResult);
            _mockMessageService.Verify(s => s.DeleteMessageAsync(int.MaxValue), Times.Once);
        }
    }
}