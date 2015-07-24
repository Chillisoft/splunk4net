using System;
using System.Threading.Tasks;

namespace splunk4net.TaskHelpers
{
    public interface IFluentTaskRunnerContinuation<T>
    {
        Task ContinueWith(Action<Task> next);
        Task<T2> ContinueWith<T2>(Func<Task<T>, T2> next);
    }
}