using System;

namespace splunk4net.Timers
{
    public interface ITimerFactory
    {
        ITimer CreateFor(Action<object> toRun, int intervalInMs = 1000, object state = null);
        ITimer CreateFor(Action toRun, int intervalInMs = 1000);
    }
}