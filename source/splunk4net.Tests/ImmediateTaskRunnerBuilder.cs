﻿using System;
using System.Threading.Tasks;
using NSubstitute;
using splunk4net.TaskHelpers;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable UnusedMember.Global

namespace splunk4net.Tests
{
    public class ImmediateTaskRunnerBuilder
    {
        private ITaskRunner _taskRunner;

        public ImmediateTaskRunnerBuilder()
        {
            WithTaskRunner(Substitute.For<ITaskRunner>())
                .WithDefaultHandlers();
        }

        public ImmediateTaskRunnerBuilder WithDefaultHandlers()
        {
            _taskRunner.Run(Arg.Any<Action>())
                .ReturnsForAnyArgs(ci =>
                {
                    var task = Task.Run(ci.Arg<Action>());
                    task.Wait();
                    return task;
                });
            _taskRunner.CreateNotStartedFor(Arg.Any<Action>())
                .ReturnsForAnyArgs(ci => new Task(ci.Arg<Action>()));
            _taskRunner.Continue(Arg.Any<Task>())
                .ReturnsForAnyArgs(ci => CreateImmediateContinuationFor(ci.Arg<Task>()));
            return WithSupportForTaskOfType<bool>();
        }

        public static Task<T> CreateImmediateTaskFor<T>(Func<T> func)
        {
            var task = Task.Run(func);
            task.Wait();
            return task;
        }

        public static IContinuation CreateImmediateContinuationFor(Task initialTask)
        {
            var continuation = Substitute.For<IContinuation>();
            continuation.With(Arg.Any<Action<Task>>())
                .ReturnsForAnyArgs(ci =>
                {
                    var action = ci.Arg<Action<Task>>();
                    var nextTask = Task.Run(() => action(initialTask));
                    nextTask.Wait();
                    return nextTask;
                });
            return continuation;
        }

        public static IContinuation<T> CreateImmediateContinuationFor<T,TNext>(Task<T> initialTask)
        {
            var continuation = Substitute.For<IContinuation<T>>();
            continuation.With(Arg.Any<Func<Task<T>, TNext>>())
                .ReturnsForAnyArgs(ci =>
                {
                    initialTask.Wait();
                    var action = ci.Arg<Func<Task<T>, TNext>>();
                    var nextTask = Task.Run(() => action(initialTask));
                    nextTask.Wait();
                    return nextTask;
                });
            return continuation;
        }

        // ReSharper disable once UnusedMember.Global
        public ImmediateTaskRunnerBuilder WithNotStartedHandlerFor<T>()
        {
            _taskRunner.CreateNotStartedFor(Arg.Any<Func<T>>())
                .ReturnsForAnyArgs(ci => new Task<T>(ci.Arg<Func<T>>()));
            return this;
        }

        public ImmediateTaskRunnerBuilder WithTaskRunner(ITaskRunner taskRunner)
        {
            _taskRunner = taskRunner;
            return this;
        }

        public ImmediateTaskRunnerBuilder WithSupportForTaskOfType<T>()
        {
            _taskRunner.Run(Arg.Any<Func<T>>())
                .ReturnsForAnyArgs(ci =>
                {
                    var task = Task.Run(() => ci.Arg<Func<T>>()());
                    task.Wait();
                    return task;
                });
            _taskRunner.CreateNotStartedFor(Arg.Any<Func<T>>())
                .ReturnsForAnyArgs(ci => new Task<T>(ci.Arg<Func<T>>()));
            return this;
        }

        public ImmediateTaskRunnerBuilder WithSupportForContinuationOfType<T,TNext>()
        {
            _taskRunner.Continue(Arg.Any<Task<T>>())
                .ReturnsForAnyArgs(ci => CreateImmediateContinuationFor<T,TNext>(ci.Arg<Task<T>>()));
            return this;
        }

        public ITaskRunner Build()
        {
            return _taskRunner;
        }

        public static ImmediateTaskRunnerBuilder Create()
        {
            return new ImmediateTaskRunnerBuilder();
        }

        public static ITaskRunner BuildDefault()
        {
            return Create().Build();
        }
    }
}
