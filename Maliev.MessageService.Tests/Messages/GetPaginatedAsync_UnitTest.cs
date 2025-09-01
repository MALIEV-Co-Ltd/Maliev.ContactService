// <copyright file="GetPaginatedAsync_UnitTest.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Maliev.MessageService.Tests.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    public class GetPaginatedAsync_UnitTest
    {
        private readonly Mock<IMessageService> _mockMessageService;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetPaginatedAsync_UnitTest" /> class.
        /// </summary>
        public GetPaginatedAsync_UnitTest()
        {
            _mockMessageService = new Mock<IMessageService>();
        }

        /// <summary>
        /// First page, should have no previous page.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task FirstPage_ShouldReturnHasNoPreviousPage()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 1000; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, FirstName = "FN", LastName = "LN", Company = "C", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages, 1000, 1, 100);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, string.Empty, 1, 100))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, string.Empty, 1, 100);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.False(returnedList.HasPreviousPage);
            Assert.True(returnedList.HasNextPage);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, string.Empty, 1, 100), Times.Once);
        }

        /// <summary>
        /// Last page, should have no next page.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task LastPage_ShouldReturnHasNoNextPage()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 1000; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, FirstName = "FN", LastName = "LN", Company = "C", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages, 1000, 10, 100);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, string.Empty, 10, 100))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, string.Empty, 10, 100);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.True(returnedList.HasPreviousPage);
            Assert.False(returnedList.HasNextPage);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, string.Empty, 10, 100), Times.Once);
        }

        /// <summary>
        /// No messages, should return not found.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task NoMessages_ShouldReturnNotFound()
        {
            // Arrange
            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, string.Empty, null, null))
                .ReturnsAsync((PaginatedListDto<MessageDto>)null);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, string.Empty, null, null);

            // Assert
            Assert.IsType<NotFoundResult>(actionResult.Result);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, string.Empty, null, null), Times.Once);
        }

        /// <summary>
        /// No query, should all messages.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task NoQuery_ShouldReturnAllMessages()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 1000; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, FirstName = "FN", LastName = "LN", Company = "C", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages, 1000, 1, 1000);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, string.Empty, null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, string.Empty, null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(1000, returnedList.TotalRecords);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, string.Empty, null, null), Times.Once);
        }

        /// <summary>
        /// Page size defined, should return ten pages.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task PageSizeDefined_ShouldReturnTenPages()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 1000; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, FirstName = "FN", LastName = "LN", Company = "C", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages, 1000, 1, 100);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, string.Empty, null, 100))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, string.Empty, null, 100);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(10, returnedList.TotalPages);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, string.Empty, null, 100), Times.Once);
        }

        /// <summary>
        /// Search by company, should return matched records.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task SearchByCompany_ShouldReturnMatchedRecords()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 20; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, Company = "HelloWorld", FirstName = "FN", LastName = "LN", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages, 20, 1, 10);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, "hello", null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, "hello", null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(20, returnedList.Items.Count);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, "hello", null, null), Times.Once);
        }

        /// <summary>
        /// Search by email, should return matched records.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task SearchByEmail_ShouldReturnMatchedRecords()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 20; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, Email = "HelloWorld", FirstName = "FN", LastName = "LN", Company = "C", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages, 20, 1, 10);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, "hello", null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, "hello", null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(20, returnedList.Items.Count);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, "hello", null, null), Times.Once);
        }

        /// <summary>
        /// Search by first name, should return matched records.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task SearchByFirstName_ShouldReturnMatchedRecords()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 20; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, FirstName = "HelloWorld", LastName = "LN", Company = "C", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages, 20, 1, 10);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, "hello", null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, "hello", null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(20, returnedList.Items.Count);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, "hello", null, null), Times.Once);
        }

        /// <summary>
        /// Search by id, should return matched records.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task SearchById_ShouldReturnMatchedRecords()
        {
            // Arrange
            var messages = new List<MessageDto>();
            messages.Add(new MessageDto { Id = 512, FirstName = "FN", LastName = "LN", Company = "C", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            var paginatedList = new PaginatedListDto<MessageDto>(messages, 1, 1, 10);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, "512", null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, "512", null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Single(returnedList.Items);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, "512", null, null), Times.Once);
        }

        /// <summary>
        /// Search by last name, should return matched records.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task SearchByLastName_ShouldReturnMatchedRecords()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 20; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, LastName = "HelloWorld", FirstName = "FN", Company = "C", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages, 20, 1, 10);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, "hello", null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, "hello", null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(20, returnedList.Items.Count);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, "hello", null, null), Times.Once);
        }

        /// <summary>
        /// Search by message content, should return matched records.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task SearchByMessageContent_ShouldReturnMatchedRecords()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 20; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, MessageContent = "HelloWorld", FirstName = "FN", LastName = "LN", Company = "C", Email = "E", Telephone = "T", Country = "C" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages, 20, 1, 10);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, "hello", null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, "hello", null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(20, returnedList.Items.Count);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, "hello", null, null), Times.Once);
        }

        /// <summary>
        /// Search by telephone, should return matched records.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task SearchByTelephone_ShouldReturnMatchedRecords()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 20; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, Telephone = "HelloWorld", FirstName = "FN", LastName = "LN", Company = "C", Email = "E", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages, 20, 1, 10);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(null, "hello", null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(null, "hello", null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(20, returnedList.Items.Count);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(null, "hello", null, null), Times.Once);
        }

        /// <summary>
        /// Sort by created date ascending, should return sorted messages.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task SortByCreatedDateAscending_ShouldReturnSortedMessages()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i <= 1000; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, CreatedDate = new DateTime(2000 + i, 1, 1), FirstName = "FN", LastName = "LN", Company = "C", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages.OrderBy(m => m.CreatedDate).ToList(), 1001, 1, 100);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(MessageSortType.MessageCreatedDate_Ascending, string.Empty, null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(MessageSortType.MessageCreatedDate_Ascending, string.Empty, null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(2000, returnedList.Items.First().CreatedDate.Value.Year);
            Assert.Equal(3000, returnedList.Items.Last().CreatedDate.Value.Year);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(MessageSortType.MessageCreatedDate_Ascending, string.Empty, null, null), Times.Once);
        }

        /// <summary>
        /// Sort by created date descending, should return sorted messages.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task SortByCreatedDateDescending_ShouldReturnSortedMessages()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i <= 1000; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, CreatedDate = new DateTime(2000 + i, 1, 1), FirstName = "FN", LastName = "LN", Company = "C", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages.OrderByDescending(m => m.CreatedDate).ToList(), 1001, 1, 100);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(MessageSortType.MessageCreatedDate_Descending, string.Empty, null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(MessageSortType.MessageCreatedDate_Descending, string.Empty, null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(3000, returnedList.Items.First().CreatedDate.Value.Year);
            Assert.Equal(2000, returnedList.Items.Last().CreatedDate.Value.Year);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(MessageSortType.MessageCreatedDate_Descending, string.Empty, null, null), Times.Once);
        }

        /// <summary>
        /// Sort by Id ascending, should return sorted messages.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task SortByIdAscending_ShouldReturnSortedMessages()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 1000; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, FirstName = "FN", LastName = "LN", Company = "C", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages.OrderBy(m => m.Id).ToList(), 1000, 1, 100);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(MessageSortType.MessageId_Ascending, string.Empty, null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(MessageSortType.MessageId_Ascending, string.Empty, null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(1, returnedList.Items.First().Id);
            Assert.Equal(1000, returnedList.Items.Last().Id);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(MessageSortType.MessageId_Ascending, string.Empty, null, null), Times.Once);
        }

        /// <summary>
        /// Sort by Id descending, should return sorted messages.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task SortByIdDescending_ShouldReturnSortedMessages()
        {
            // Arrange
            var messages = new List<MessageDto>();
            for (int i = 0; i < 1000; i++)
            {
                messages.Add(new MessageDto { Id = i + 1, FirstName = "FN", LastName = "LN", Company = "C", Email = "E", Telephone = "T", Country = "C", MessageContent = "MC" });
            }
            var paginatedList = new PaginatedListDto<MessageDto>(messages.OrderByDescending(m => m.Id).ToList(), 1000, 1, 100);

            _mockMessageService.Setup(s => s.GetPaginatedAsync(MessageSortType.MessageId_Descending, string.Empty, null, null))
                .ReturnsAsync(paginatedList);

            var controller = new MessagesController(_mockMessageService.Object);

            // Act
            var actionResult = await controller.GetPaginatedAsync(MessageSortType.MessageId_Descending, string.Empty, null, null);

            // Assert
            var okResult = Assert.IsType<ActionResult<PaginatedListDto<MessageDto>>>(actionResult);
            var returnedList = Assert.IsType<PaginatedListDto<MessageDto>>(okResult.Value);
            Assert.Equal(1000, returnedList.Items.First().Id);
            Assert.Equal(1, returnedList.Items.Last().Id);
            _mockMessageService.Verify(s => s.GetPaginatedAsync(MessageSortType.MessageId_Descending, string.Empty, null, null), Times.Once);
        }
    }
}