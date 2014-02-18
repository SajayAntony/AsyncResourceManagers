using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace Microsoft.Resource.Runtime.Test
{
    [TestClass]
    public class ThrottleTests
    {
        [TestMethod]
        public void SingleItem()
        {
            AsyncThrottle<object> throttle = new AsyncThrottle<object>(1);
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            bool acquired = throttle.Acquire(_ => { }, this);
            bool acquiredSecond = throttle.Acquire(_ => { tcs.SetResult(null); }, this);
            Assert.AreEqual(acquiredSecond, false);
            throttle.Release();
            var result = tcs.Task.Result;
            Assert.AreEqual(result, null);
        }


        [TestMethod]
        public void Multiple()
        {
            int maxConcurrent = 10;
            const int numberOfItems = 50;
            int itemProcessTime = 2; // Second

            // warmup
            Console.Write("Warmup: ");
            ProcessConcurrent(maxConcurrent, numberOfItems, itemProcessTime);

            // Measurement
            Console.Write("Measure: ");
            ProcessConcurrent(maxConcurrent, numberOfItems, itemProcessTime);
        }


        [TestMethod]
        public void SingleNoOp()
        {
            int maxConcurrent = 1;
            const int numberOfItems = 5 * 1000 * 1000;
            int itemProcessTime = 0; // Second

            // warmup
            Console.Write("Warmup: ");
            ProcessConcurrent(maxConcurrent, numberOfItems, itemProcessTime);

            // Measurement
            Console.Write("Measure: ");
            ProcessConcurrent(maxConcurrent, numberOfItems, itemProcessTime);
        }

        private void ProcessConcurrent(int maxConcurrent, int numberOfItems, int sleepTimeSeconds)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            AsyncThrottle<object> throttle = new AsyncThrottle<object>(maxConcurrent);
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            int processed = 0;
            Action<object> onAcquireCallback = (s) =>
            {
                var p = Interlocked.Increment(ref processed);
                if (sleepTimeSeconds > 0)
                {
                    Thread.Sleep(sleepTimeSeconds * 1000);
                }
                throttle.Release();

                if (p == numberOfItems)
                {
                    tcs.SetResult(null);
                }
            };


            for (int i = 0; i < numberOfItems; i++)
            {
                if (throttle.Acquire(onAcquireCallback, this))
                {
                    IoThreadScheduler.ScheduleCallback(onAcquireCallback, null);
                }
            }

            var result = tcs.Task.Result;
            Assert.AreEqual(result, null);
            Assert.AreEqual(processed, numberOfItems);
            watch.Stop();
            double requestRate = ((double)numberOfItems / watch.Elapsed.TotalSeconds);
            Console.WriteLine("Request Rate = {0:N2} request/sec", requestRate);
        }

        [TestMethod]
        public void TestAwaitableThrottle()
        {
            int maxConcurrent = Environment.ProcessorCount *10;
            int processedItems = 0;
            int numberOfItems = 5 * 1000 * 1000;
            var tcs = new TaskCompletionSource<bool>();
            var throttle = new AwaitableThrottle(maxConcurrent);


            var watch = new Stopwatch();
            watch.Start();
            for (int i = 0; i < Environment.ProcessorCount; i++)
                Task.Run(async () =>
                {
                    while (true)
                    {
                        await throttle;
                        Interlocked.Increment(ref processedItems);
                        if (processedItems >= numberOfItems)
                        {
                            break;
                        }

                        //Offload to ensure simulation.
                        Task.Run(() => throttle.Release());
                    }

                    tcs.SetResult(true);
                });

            tcs.Task.Wait();

            double requestRate = ((double)numberOfItems / watch.Elapsed.TotalSeconds);
            Console.WriteLine("Request Rate = {0:N2} request/sec", requestRate);
        }
    }
}
