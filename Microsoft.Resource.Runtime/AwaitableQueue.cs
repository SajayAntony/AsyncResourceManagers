using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Resource.Runtime
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AwaitableQueue<T>
    {
        readonly Queue<QueuedAwaiter<T>> _readers = new Queue<QueuedAwaiter<T>>();
        readonly Queue<T> _outputs = new Queue<T>();

        object ThisLock
        {
            get { return _readers; }
        }

        #region Awaitables

        public QueuedAwaiter<T> GetAwaiter()
        {

            // If there is an output available then get the value immediately             
            lock (ThisLock)
            {
                if (_outputs.Count > 0)
                {
                    var result = _outputs.Dequeue();
                    return QueuedAwaiter<T>.GetCompletedAwaiter(result);
                }
                else
                {
                    var awaiter = QueuedAwaiter<T>.GetAwaiter();
                    _readers.Enqueue(awaiter);
                    return awaiter;
                }
            }
        }

        #endregion

        internal void Enqueue(T result)
        {
            // Writer is enqueing an object and 
            // if there is a reader we need to complete any reader. 
            // else we need to park the result into the queue.             
            QueuedAwaiter<T> awaiter = default(QueuedAwaiter<T>);
            bool complete = false;
            lock (ThisLock)
            {
                if (_readers.Count > 0)
                {
                    awaiter = _readers.Dequeue();
                    complete = true;
                }
                else
                {
                    _outputs.Enqueue(result);
                }
            }

            if (complete)
            {
                awaiter.Complete(result);
            }
        }
    }

    /// <summary>
    /// The QueuedAwaiter is a ValueType. A completed QueuedAwaiter will not 
    /// have a schedulable continuation where are as one that is not completed 
    /// will have a threadsafe schedulable continuation. 
    /// As it is a value type the result for the awaiter will be on the stack 
    /// while one that is scheduled will be on the continuation which is a ref type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct QueuedAwaiter<T> : INotifyCompletion
    {
        private readonly QueuedAwaiterContinuation<T> _continuation;
        private bool _isCompleted;
        private readonly T _completedResultOnStack;

        private QueuedAwaiter(bool isComplete, T result = default(T))
        {
            _isCompleted = isComplete;
            _completedResultOnStack = result;
            _continuation = !_isCompleted ? new QueuedAwaiterContinuation<T>() : null;
        }

        public T GetResult()
        {
            return _continuation == null ? _completedResultOnStack : _continuation.Result;
        }

        public bool IsCompleted
        {
            get { return _isCompleted; }
        }

        public void OnCompleted(Action continuation)
        {
            if (_continuation == null)
            {
                throw new InvalidOperationException("OnCompleted cannot be scheduled as the awaiter is already completed.");
            }

            _continuation.OnCompleted(continuation);
        }

        internal static QueuedAwaiter<T> GetCompletedAwaiter(T result)
        {
            return new QueuedAwaiter<T>(true, result);
        }

        internal static QueuedAwaiter<T> GetAwaiter()
        {
            return new QueuedAwaiter<T>(false);
        }

        internal void Complete(T result)
        {
            if (_isCompleted)
            {
                throw new InvalidOperationException("Cannot call Complete() on an already completed awaiter.");
            }

            if (_continuation == null)
            {
                throw new InvalidOperationException("Should not complete an awaiter without a continuation");
            }

            _isCompleted = true;
            _continuation.Result = result;
            _continuation.Complete();
        }
    }

    /// <summary>
    /// This class helps ensure that the completion runs only one time and holds the result 
    /// incase of a race between the awaiter being called and completion. 
    /// The result will have the value when the awaiter is completed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class QueuedAwaiterContinuation<T> : INotifyCompletion
    {
        private Action _continuation;
        private static Action SENTINEL = () => { };

        public T Result { get; set; }

        public void OnCompleted(Action continuation)
        {
            if (_continuation == SENTINEL || (Interlocked.CompareExchange(ref _continuation, continuation, null) == SENTINEL))
            {
                Task.Run(continuation);
            }
        }

        public void Complete()
        {
            var prev = _continuation ?? Interlocked.CompareExchange(ref _continuation, SENTINEL, null);
            if (prev != null)
            {
                prev();
            }
        }
    }
}
