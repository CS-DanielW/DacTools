// Copyright (c) 2020 DrBarnabus

using System;
using System.Threading;
using System.Threading.Tasks;
using DacTools.Deployment.Core.AsyncTasks;
using DacTools.Deployment.Core.Common;
using DacTools.Deployment.Core.DatabaseListGenerators;
using DacTools.Deployment.Core.Logging;
using DacTools.Deployment.Core.Tests.TestInfrastructure;
using Moq;
using Shouldly;
using Xunit;

namespace DacTools.Deployment.Core.Tests.AsyncTasks
{
    public class AsyncTaskBaseTests
    {
        [Fact]
        public async Task ShouldCallTheCorrectLoggingMethods()
        {
            // Setup
            var arguments = new Arguments();
            var mockLog = new Mock<ILog>();
            var mockBuildServerResolver = new Mock<IBuildServerResolver>();
            var sut = new TestAsyncTask2(arguments, mockLog.Object, mockBuildServerResolver.Object);
            sut.Setup(new DatabaseInfo(1, "Test"), (_, __, ___) => { });

            // Act
            await sut.Run(CancellationToken.None);

            // Assert
            mockLog.Verify(m => m.Write(LogLevel.Debug, @"'Test:1' Internal - Test"), Times.Once);
            mockLog.Verify(m => m.Write(LogLevel.Info, "'Test:1' Internal - Test"), Times.Once);
            mockLog.Verify(m => m.Write(LogLevel.Warn, "'Test:1' Internal - Test"), Times.Once);
            mockLog.Verify(m => m.Write(LogLevel.Error, "'Test:1' Internal - Test"), Times.Once);
        }

        [Fact]
        public async Task ShouldSetTheProgressUpdateDelegateCorrectly()
        {
            // Setup
            bool delegateCalled = false;
            var databaseInfo = new DatabaseInfo(1, "Test");

            var mockLog = new Mock<ILog>();
            var arguments = new Arguments();
            var mockBuildServerResolver = new Mock<IBuildServerResolver>();
            var sut = new TestAsyncTask2(arguments, mockLog.Object, mockBuildServerResolver.Object);

            void ProgressUpdate(AsyncTaskBase asyncTask, bool succeeded, long elapsedMiliseconds)
            {
                delegateCalled = true;
                asyncTask.DatabaseInfo.ShouldBe(databaseInfo);
                succeeded.ShouldBeTrue();
                elapsedMiliseconds.ShouldBe(100);
            }

            sut.Setup(databaseInfo, ProgressUpdate);

            // Act
            await sut.Run(CancellationToken.None);

            // Assert
            delegateCalled.ShouldBeTrue();
            sut.PublicProgressUpdate.ShouldBe(ProgressUpdate);
        }

        [Fact]
        public void ShouldThrowAnArgumentNullExceptionWhenDatabaseInfoIsNull()
        {
            // Setup
            var mockLog = new Mock<ILog>();
            var arguments = new Arguments();
            var mockBuildServerResolver = new Mock<IBuildServerResolver>();
            var sut = new TestAsyncTask2(arguments, mockLog.Object, mockBuildServerResolver.Object);

            // Assert
            Assert.Throws<ArgumentNullException>(() => sut.Setup(null, (_, __, ___) => { }));
        }

        [Fact]
        public void ShouldThrowAnArgumentNullExceptionWhenProgressUpdateDelegateIsNull()
        {
            // Setup
            var mockLog = new Mock<ILog>();
            var arguments = new Arguments();
            var mockBuildServerResolver = new Mock<IBuildServerResolver>();
            var sut = new TestAsyncTask2(arguments, mockLog.Object, mockBuildServerResolver.Object);

            // Assert
            Assert.Throws<ArgumentNullException>(() => sut.Setup(new DatabaseInfo(1, "Test"), null));
        }
    }
}
