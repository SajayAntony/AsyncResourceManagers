using System;

namespace Microsoft.Resource.Runtime
{
    /// <summary>
    /// The resource manager class is able to accept work items
    /// and schedule as and when resources are available. 
    /// </summary>
    public class AsyncResourceManager<TState,TResource>
    {
        /// <summary>
        /// Take returns a resource and does not callback if completed synchronously. 
        /// Otherwise it would invoke a callback with the passed in state and resource 
        /// once it become availble to do the work. 
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public AsyncCompletion Take(Action<TResource,TState> callback, TState state, out TResource resource)
        {
            resource = default(TResource);
            return AsyncCompletion.Completed;
        }

        /// <summary>
        /// Return allows the caller to return a resource back to the pool. 
        /// This can trigger processing of any items waiting for resources.
        /// </summary>
        public void Return(TResource resouce)
        {
 
        }
    }
}
