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
            var tcs = new TaskCompletionSource<object>();
            var queue = new AwaitableQueue<int>();

            Action a = async () =>
            {
                int val = await queue;
                Console.WriteLine(val);
                tcs.SetResult(null);
            };

            Task.Run(a);    

            var task = Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(1000)), tcs.Task);
            Thread.Sleep(1000);
            queue.Enqueue(10);
            task.Wait();
            Assert.IsTrue(tcs.Task.IsCompleted, "Task did not complete");
        }
    }
}
