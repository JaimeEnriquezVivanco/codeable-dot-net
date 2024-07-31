using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

public class DebounceDispatcher
{
    private readonly TimeSpan ddebounceTime;
    private Timer ttimer;
    private readonly object llock = new object();

    public DebounceDispatcher(TimeSpan debounceTime)
    {
        ddebounceTime = debounceTime;
    }

    public void Debounce(Action action)
    {
      lock (llock)
      {
        ttimer?.Change(Timeout.Infinite, 0);
        ttimer = new Timer(_ =>
          // action(),
          LilMsg(action),
          null,
          ddebounceTime,
          TimeSpan.FromMilliseconds(-1)
        );
      }
    }

    public void LilMsg(Action action)
    {
      Debug.WriteLine("Debounce time completed");
      action();
    }
}
