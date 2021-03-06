﻿#region copyright

// Autofac Quartz integration
// https://github.com/alphacloud/Autofac.Extras.Quartz
// Licensed under MIT license.
// Copyright (c) 2014 Alphacloud.Net

#endregion

namespace Autofac.Extras.Quartz.Tests
{
    using System;
    using FluentAssertions;
    using global::Quartz;
    using global::Quartz.Spi;
    using Moq;
    using NUnit.Framework;


    [TestFixture]
    internal class JobWrapperTests
    {
        [SetUp]
        public void SetUp()
        {
            var cb = new ContainerBuilder();
            _state = new State();

            cb.RegisterInstance(_state);
            cb.RegisterType<WrappedJob>().InstancePerLifetimeScope();

            _jobDetail = new Mock<IJobDetail>();
            _jobDetail.SetupAllProperties();
            _executionContext = new Mock<IJobExecutionContext>();
            _executionContext.Setup(c => c.JobDetail)
                .Returns(_jobDetail.Object);

            _trigger = new Mock<IOperableTrigger>();
            _calendar = new Mock<ICalendar>();

            _container = cb.Build();

            _bundle = new TriggerFiredBundle(_jobDetail.Object, _trigger.Object, _calendar.Object, false, null,
                null, null, null);

            _wrapper = new AutofacJobFactory.JobWrapper(_bundle, _container.Resolve<ILifetimeScope>(), "nested-scope");
        }


        [TearDown]
        public void TearDown()
        {
            if (_container != null)
                _container.Dispose();
        }


        private class State
        {
            public bool WasDisposed { get; set; }
            public bool WasExecuted { get; set; }
        }


        private class WrappedJob : IJob, IDisposable
        {
            private readonly State _state;


            public WrappedJob(State state)
            {
                _state = state;
            }


            public void Dispose()
            {
                _state.WasDisposed = true;
            }


            public void Execute(IJobExecutionContext context)
            {
                _state.WasExecuted = true;
            }
        }


        private IContainer _container;
        private Mock<IJobExecutionContext> _executionContext;
        private Mock<IOperableTrigger> _trigger;
        private Mock<ICalendar> _calendar;
        private TriggerFiredBundle _bundle;
        private AutofacJobFactory.JobWrapper _wrapper;
        private Mock<IJobDetail> _jobDetail;
        private State _state;


        [Test]
        public void ShouldCreateNestedScope()
        {
            string nestedLifetimeScopeTag = null;
            int nestedScopeWasDisposCount = 0;

            _container.ChildLifetimeScopeBeginning += (sender, args) => {
                nestedLifetimeScopeTag = args.LifetimeScope.Tag.ToString();
                args.LifetimeScope.CurrentScopeEnding += (o, eventArgs) => nestedScopeWasDisposCount++;
            };
            _jobDetail.SetupGet(d => d.JobType).Returns(typeof (WrappedJob));

            _wrapper.Execute(_executionContext.Object);

            nestedLifetimeScopeTag.Should().NotBeNull();
            nestedScopeWasDisposCount.Should().BeGreaterThan(0);
        }


        [Test]
        public void ShouldDisposeJobAndScopeAfterExecution()
        {
            _jobDetail.SetupGet(d => d.JobType).Returns(typeof (WrappedJob));

            _wrapper.Execute(_executionContext.Object);

            _state.WasDisposed.Should().BeTrue("Job should be disposed with nested scope");
            _state.WasExecuted.Should().BeTrue("Job was not executed");
        }
    }
}