using System.Threading;

namespace splunk4net.Timers
{
    public class TimerFacade: ITimer
    {
        private readonly Timer _actual;

        public TimerFacade(Timer actual)
        {
            _actual = actual;
        }
        public void Dispose()
        {
            _actual.Dispose();
        }
    }
}