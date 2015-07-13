﻿using System;
using System.Threading;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using Moq;
using Xunit;

namespace Hangfire.Core.Tests.Server
{
    public class SchedulePollerFacts
    {
        private const string JobId = "id";
        private readonly Mock<IStorageConnection> _connection;
        private readonly Mock<IStateMachine> _stateMachine;
        private readonly Mock<IStateMachineFactory> _stateMachineFactory;
        private readonly BackgroundProcessContextMock _context;

        public SchedulePollerFacts()
        {
            _context = new BackgroundProcessContextMock();
            _context.CancellationTokenSource.Cancel();

            _connection = new Mock<IStorageConnection>();
            _context.Storage.Setup(x => x.GetConnection()).Returns(_connection.Object);

            _stateMachine = new Mock<IStateMachine>();
			
            _stateMachineFactory = new Mock<IStateMachineFactory>();
            _stateMachineFactory.Setup(x => x.Create(It.IsNotNull<IStorageConnection>()))
                .Returns(_stateMachine.Object);

            _connection.Setup(x => x.GetFirstByLowestScoreFromSet(
                "schedule", 0, It.Is<double>(time => time > 0))).Returns(JobId);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenStateMachineFactoryIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SchedulePoller(Timeout.InfiniteTimeSpan, null));

            Assert.Equal("stateMachineFactory", exception.ParamName);
        }

        [Fact]
        public void Execute_MovesJobStateToEnqueued()
        {
            var scheduler = CreateScheduler();

			scheduler.Execute(_context.Object);

            _stateMachine.Verify(x => x.ChangeState(
                JobId,
                It.IsAny<EnqueuedState>(),
                new[] { ScheduledState.StateName },
                It.IsAny<CancellationToken>()));
            _connection.Verify(x => x.Dispose());
        }

        [Fact]
        public void Execute_DoesNotCallStateMachine_IfThereAreNoJobsToEnqueue()
        {
            _connection.Setup(x => x.GetFirstByLowestScoreFromSet(
                "schedule", 0, It.Is<double>(time => time > 0))).Returns((string)null);
            var scheduler = CreateScheduler();

			scheduler.Execute(_context.Object);

            _stateMachine.Verify(
                x => x.ChangeState(It.IsAny<string>(), It.IsAny<IState>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private SchedulePoller CreateScheduler()
        {
            return new SchedulePoller(Timeout.InfiniteTimeSpan, storage => _stateMachineFactory.Object);
        }
    }
}
