using System;
using System.Threading;

namespace splunk4net.Timers
{
    public class TimerFactory: ITimerFactory
    {
        public ITimer CreateFor(Action<object> toRun, int intervalInMs = 1000, object state = null)
        {
            var timer = new Timer(new TimerCallback(toRun), 
                                    state, 
                                    TimeSpan.FromMilliseconds(0),
                                    TimeSpan.FromMilliseconds(intervalInMs));
            return new TimerFacade(timer);
        }

        public ITimer CreateFor(Action toRun, int intervalInMs = 1000)
        {
            return CreateFor(o => toRun(), intervalInMs);
        }
    }
}
