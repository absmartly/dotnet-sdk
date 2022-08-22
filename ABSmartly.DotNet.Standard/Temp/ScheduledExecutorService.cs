using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ABSmartlySdk.Temp;

// Source: https://blog.adamfurmanek.pl/2018/08/18/trivial-scheduledthreadpoolexecutor-in-c/
public class ScheduledThreadPoolExecutor : IScheduledExecutorService
{
    public int ThreadCount => threads.Length;
    public EventHandler< Exception> OnException;

    private readonly ManualResetEvent waiter;
    private readonly Thread[] threads;
    private readonly SortedSet< Tuple< DateTime, Action>> queue;

    public ScheduledThreadPoolExecutor(int threadCount)
    {
        waiter = new ManualResetEvent(false);
        queue = new SortedSet<Tuple<DateTime, Action>>();
        OnException += (o, e) => { };
        threads = Enumerable.Range(0, threadCount).Select(i => new Thread(RunLoop)).ToArray();
        foreach(var thread in threads)
        {
            thread.Start();
        }
    }

    private void RunLoop()
    {
        while (true)
        {
            var sleepingTime = TimeSpan.MaxValue;
            var needToSleep = true;
            Action task = null;

            try
            {
                lock (waiter)
                {
                    if (queue.Any())
                    {
                        if(queue.First().Item1 <= DateTime.Now)
                        {
                            task = queue.First().Item2;
                            queue.Remove(queue.First());
                            needToSleep = false;
                        }
                        else
                        {
                            sleepingTime = queue.First().Item1 - DateTime.Now;
                        }
                    }
                }

                if (needToSleep)
                {
                    waiter.WaitOne((int)sleepingTime.TotalMilliseconds);
                }
                else
                {
                    task();
                }
            }
            catch (Exception e)
            {
                OnException(task, e);
            }
        }
    }
}