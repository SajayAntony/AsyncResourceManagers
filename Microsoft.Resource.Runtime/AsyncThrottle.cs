using System;
using System.Collections.Generic;

namespace Microsoft.Resource.Runtime
{
    class AsyncThrottle<T>
    {
        readonly object _thisLock = new object();
        readonly int _maxItems;
        int _count;
        readonly Queue<CallabackContext<T>> _pending;
        readonly Action<object> _iocallback;

        public AsyncThrottle(int max)
        {
            _count = 0;
            _maxItems = max;
            _pending = new Queue<CallabackContext<T>>();
            _iocallback = new Action<object>(IoCallback);
        }

        /// <summary>
        /// Acquire throttle will return true if there is an avaiable 
        /// slot. If there is not slot then it will queue up the callback
        /// and state to be dequeued when there is a slot avaiable. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool Acquire(Action<T> callback, T state)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("callback");
            }

            bool acquired = false;

            // Performance sensitive allocate outside the lock. 
            var context = new CallabackContext<T>
                    {
                        Callback = callback,
                        State = state
                    };

            lock (_thisLock)
            {
                if (_count < _maxItems)
                {
                    _count++;
                    acquired = true;
                }
                else
                {
                    _pending.Enqueue(context);
                }
            }

            return acquired;
        }

        private bool TryDequeue(out CallabackContext<T> item)
        {
            bool dequeued = false;
            item = default(CallabackContext<T>);

            lock (_thisLock)
            {
                if (_count < _maxItems)
                {
                    if (_pending.Count > 0)
                    {
                        //Acquire throttle 
                        _count++;
                        dequeued = true;
                        item = _pending.Dequeue();
                    }
                }
            }

            return dequeued;
        }

        /// <summary>
        /// Release will post a work item into the IOThread Scheduler that 
        /// will dequeue and call a pending item from the queue.
        /// </summary>
        public void Release()
        {
            bool schedulePending = false;
            lock (_thisLock)
            {
                _count--;
                schedulePending = _pending.Count > 0;
            }

            if (schedulePending)
            {
                IoThreadScheduler.ScheduleCallback(_iocallback, null);
            }
        }

        private void IoCallback(object state)
        {
            CallabackContext<T> item;
            if (TryDequeue(out item))
            {
                item.Callback(item.State);
            }
        }

        private CallabackContext<T> Dequeue()
        {
            CallabackContext<T> item;
            lock (_thisLock)
            {
                item = _pending.Dequeue();
            }

            return item;
        }

        struct CallabackContext<U>
        {
            public Action<T> Callback;
            public T State;
        }
    }
}
