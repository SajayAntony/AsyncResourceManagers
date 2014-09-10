using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Resource.Runtime.Test
{
    [TestClass]
    public class QueueTest
    {
        [TestMethod]
        public void Test1Item()
        {
            TestItem(1);
        }

        [TestMethod]
        public void Test2Item()
        {
            TestItem(2);
        }

        [TestMethod]
        public void Test1000Item()
        {
            TestItem(1000);
        }

        [TestMethod]
        public void Test1000000Item()
        {
            TestItem(1000 * 1000);
        }


        [TestMethod]
        public void MultipleProducers()
        {
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            AwaitableQueue<int> queue = new AwaitableQueue<int>();
            int count = 1000 * 1000;
            var t1 = Task.Run(() =>
            {
                for (int i = 0; i < count / 2; i++)
                {
                    queue.Enqueue(i * 2);
                }
            });

            var t2 = Task.Run(() =>
            {
                for (int i = 0; i < count / 2; i++)
                {
                    queue.Enqueue(i * 2 + 1);
                }
            });

            SortedSet<int> values = new SortedSet<int>();
            var t3 = Task.Run(async () =>
            {
                for (int i = 0; i < count; i++)
                {
                    var val = await queue;
                    if (values.Contains(val))
                    {
                        tcs.SetException(new InternalTestFailureException("Duplicate value encounted in the queue"));
                    }
                    values.Add(val);
                }
                Task.Delay(1000).ContinueWith(t => { tcs.SetResult(null); });
                values.Add(await queue);
            });

            var final = Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(10)), tcs.Task);
            final.Wait();
            Assert.IsFalse(tcs.Task.IsFaulted);
            Assert.IsTrue(tcs.Task.IsCompleted, "Task did not complete");
            Assert.IsTrue(values.Count == count);

        }


        [TestMethod]
        public void MultipleProducersMultipleConsumer()
        {
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            AwaitableQueue<int> queue = new AwaitableQueue<int>();
            int count = 1000 * 1000;
            ConcurrentQueue<int> values = new ConcurrentQueue<int>();
            Action onNext = () =>
            {
                if (values.Count == count)
                {
                    Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(t =>
                    {
                        tcs.SetResult(null);
                    });
                }
            };

            var producer1 = Task.Run(() =>
            {
                for (int i = 0; i < count / 2; i++)
                {
                    queue.Enqueue(i * 2);
                }
            });

            var producer2 = Task.Run(() =>
            {
                for (int i = 0; i < count / 2; i++)
                {
                    queue.Enqueue(i * 2 + 1);
                }
            });

            var consumer1 = Task.Run(async () =>
            {
                while (!tcs.Task.IsCompleted)
                {
                    values.Enqueue(await queue);
                    onNext();
                }
            });

            var consumer2 = Task.Run(async () =>
            {
                while (!tcs.Task.IsCompleted)
                {
                    values.Enqueue(await queue);
                    onNext();
                }

            });

            var final = Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(10)), tcs.Task);
            final.Wait();
            Assert.IsFalse(tcs.Task.IsFaulted);
            Assert.IsTrue(tcs.Task.IsCompleted, "Task did not complete");
            Console.WriteLine("Received {0} values", values.Count);
            Assert.IsTrue(values.Count == count);

        }

        public void TestItem(int n)
        {
            var tcs = new TaskCompletionSource<object>();
            var queue = new AwaitableQueue<int>();

            Action a = async () =>
            {
                var builder = new StringBuilder();
                int val = 0;
                for (int i = 0; i < n; i++)
                {
                    val = await queue;
                    if (val != i)
                    {
                        tcs.SetException(new InternalTestFailureException(
                            string.Format("Incorrect values received. Expecting {0} received {1}", i, val)));
                    }
                    builder.AppendFormat("{0},", val);
                }
                Console.WriteLine("Last value received {0} values", val);
                Console.WriteLine(builder.ToString());
                tcs.SetResult(null);
            };

            Task.Run(a);

            var task = Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(10)), tcs.Task);
            Thread.Sleep(100);
            for (int i = 0; i < n; i++)
            {
                queue.Enqueue(i);
            }
            Console.WriteLine("Completed enqueuing.");
            task.Wait();
            Assert.IsTrue(tcs.Task.IsCompleted, "Task did not complete");
        }
    }
}
