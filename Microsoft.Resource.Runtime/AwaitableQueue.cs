using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
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
        private QueueAwaiter<T> _awaiter;

        Queue<QueueAwaiter<T>> _readers = new Queue<QueueAwaiter<T>>();
        Queue<T> _results = new Queue<T>();

        object ThisLock
        {
            get { return _readers; }
        }

        public AwaitableQueue()
        {
            _awaiter = new QueueAwaiter<T>();
        }

        #region Awaitables

        public QueueAwaiter<T> GetAwaiter()
        {
            // The reader will await a value 
            // otherwise will get an incomplete awaiter. 
            var awaiter = new QueueAwaiter<T>();
            lock (ThisLock)
            {
                if (_results.Count == 0)
                {
                    _readers.Enqueue(awaiter);
                }
                else
                {
                    var result = _results.Dequeue();                    
                    awaiter.Complete(result, synchronous: true);
                }
            }

            return awaiter;
        }

        #endregion

        internal void Enqueue(T result)
        {
            // Writer is enqueing an object and 
            // if there is a reader we need to complete any reader. 
            // else we need to park the result into the queue. 
            QueueAwaiter<T> awaiter = new QueueAwaiter<T>();
            bool complete = false;
            lock (ThisLock)
            {
                if (_readers.Count > 0)
                {
                    _awaiter = _readers.Dequeue();
                    complete = true;
                }
                else
                {
                    _results.Enqueue(result);
                }
            }

            if (complete)
            {
                _awaiter.Complete(result);
            }
        }
    }

    public class QueueAwaiter<T> : INotifyCompletion
    {
        private Action _continuation;
        private bool _isCompleted;
        private T _result;
        private static Action SENTINEL = () => { };

        public T GetResult()
        {
            return _result;
        }

        public bool IsCompleted
        {
            get { return _isCompleted; }
        }

        public void OnCompleted(Action continuation)
        {
            if (_continuation == SENTINEL || (Interlocked.CompareExchange(ref _continuation, continuation, null) == SENTINEL))
            {
                Task.Run(continuation);
            }
        }

        public void Complete(T result, bool synchronous = false)
        {
            Contract.Assert(_isCompleted == false);
            _isCompleted = true;
            _result = result;
            if (!synchronous)
            {
                this.TryComplete();
            }
        }

        public void TryComplete()
        {
            var prev = _continuation ?? Interlocked.CompareExchange(ref _continuation, SENTINEL, null);
            if (prev != null)
            {
                prev();
            }
        }
    }
}
