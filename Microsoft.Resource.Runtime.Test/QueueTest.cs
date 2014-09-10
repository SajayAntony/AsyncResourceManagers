using System;
using System.Collections.Generic;
using System.Linq;
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



        public void TestItem(int n)
        {
            var tcs = new TaskCompletionSource<object>();
            var queue = new AwaitableQueue<int>();

            Action a = async () =>
            {
                var builder = new StringBuilder();
                for (int i = 0; i < n; i++)
                {
                    int val = await queue;
                    if (val != i)
                    {
                        tcs.SetException(new InternalTestFailureException(
                            string.Format("Incorrect values received. Expecting {0} received {1}",i, val)));
                    }
                    builder.AppendFormat("{0},", val);
                }
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
            task.Wait();
            Assert.IsTrue(tcs.Task.IsCompleted, "Task did not complete");
        }
    }
}
