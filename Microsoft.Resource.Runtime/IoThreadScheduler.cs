//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Resource.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Security;
    using System.Threading;

    class IoThreadScheduler
    {
        static readonly IoThreadScheduler Current;

        readonly CriticalHelper _helper = new CriticalHelper();

        static IoThreadScheduler()
        {
            Current = new IoThreadScheduler();
        }

        public static void ScheduleCallback(Action<object> callback, object state)
        {
            Current.Schedule(callback, state);
        }

        public void Schedule(Action<object> callback, object state)
        {
            _helper.ScheduleCallback(callback, state);
        }

        /// <SecurityNote>
        /// Execution context does not flow with the critical helper
        /// </SecurityNote>        
        class CriticalHelper
        {
            readonly object _lockObject = new object();
            bool _queuedCompletion;
            readonly Queue<WorkItem> _workQueue = new Queue<WorkItem>();
            readonly ScheduledOverlapped _overlapped; 

            public CriticalHelper()
            {
                _overlapped = new ScheduledOverlapped(new Action<object>(CompletionCallback));
            }

            public void ScheduleCallback(Action<object> callback, object state)
            {
                if (callback == null)
                    throw new ArgumentNullException("callback");

                object workItemState = state;
                var workItem = new WorkItem(callback, workItemState);

                bool needOverlappedQueued = false;
                try
                {
                    lock (_lockObject)
                    {
                        this._workQueue.Enqueue(workItem);

                        if (!_queuedCompletion)
                        {
                            needOverlappedQueued = true;
                            this._queuedCompletion = true;
                        }
                    }
                }
                finally
                {
                    if (needOverlappedQueued)
                    {
                        this._overlapped.Post();
                    }
                }
            }
            
            /// <SecurityNote>
            /// note that in some hosts this runs without any user context on the stack
            /// </SecurityNote>
            void CompletionCallback(object state)
            {
                lock (_lockObject)
                {
                    _queuedCompletion = false;
                }
                
                ProcessCallbacks();
            }

            /// <SecurityNote>
            /// note that in some hosts this runs without any user context on the stack
            /// </SecurityNote>
            void ProcessCallbacks()
            {
                while (true)
                {
                    WorkItem workItem;

                    bool needOverlappedQueued = false;
                    try
                    {
                        lock (_lockObject)
                        {
                            if (this._workQueue.Count != 0)
                            {
                                workItem = this._workQueue.Dequeue();
                            }                          
                            else
                            {
                                break;
                            }

                            if (!_queuedCompletion && (this._workQueue.Count > 0))
                            {
                                needOverlappedQueued = true;
                                this._queuedCompletion = true;
                            }
                        }
                    }
                    finally
                    {
                        if (needOverlappedQueued)
                        {
                            this._overlapped.Post();
                        }
                    }

                    workItem.Invoke();
                }
            }


            struct WorkItem
            {
                readonly Action<object> _callback;
                readonly object _state;                

                public WorkItem(Action<object> callback, object state)
                {
                    this._callback = callback;
                    this._state = state;
                }

                /// <SecurityNote>
                /// note that in some hosts this runs without any user context on the stack
                /// </SecurityNote>
                public void Invoke()
                {
                    this._callback(_state);
                }
            }

            class ScheduledOverlapped
            {
                readonly unsafe NativeOverlapped* _nativeOverlapped;
                readonly Action<object> _callback;

                // Since we keep this object in a static, we never need to Free the overlapped, so there's no need for a finalizer.
                unsafe public ScheduledOverlapped(Action<object> callback)
                {
                    var overlapped = new Overlapped(0, 0, IntPtr.Zero, null);
                    this._nativeOverlapped = overlapped.UnsafePack(new IOCompletionCallback(IoCallback), null);
                    this._callback = callback;
                }

                unsafe void IoCallback(uint errorCode, uint numBytes, NativeOverlapped* nativeOverlapped)
                {
                    _callback(null);
                }

                unsafe public void Post()
                {
                    ThreadPool.UnsafeQueueNativeOverlapped(this._nativeOverlapped);
                }
            }
        }
    }
}
