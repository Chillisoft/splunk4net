using System;
using System.Threading.Tasks;

namespace splunk4net.TaskHelpers
{
    public interface IContinuation
    {
        Task With(Action<Task> action);
    }

    public interface IContinuation<T>
    {
        // ReSharper disable once UnusedMember.Global
        Task With(Action<Task<T>> action);
        Task<TNext> With<TNext>(Func<Task<T>, TNext> func);
    }
}