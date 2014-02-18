using System;
using System.Collections.Generic;

namespace Microsoft.Resource.Memory
{
    class PreAllocBufferManager
    {
        public PreAllocBufferManager()
        {
        }
    }

    class LargeBuffer
    {
        readonly byte[] ParentArray;
        static readonly int MinSegmentSize;

        static LargeBuffer()
        {
            MinSegmentSize = IntPtr.Size;
        }

        private LargeBuffer(int maxPoolSize, int segmentSize)
        {
            if (segmentSize <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            var count = (maxPoolSize / segmentSize) + 1;
            this.ParentArray = new byte[segmentSize * count];
        }

        public static bool Is64BitProcess()
        {
            return IntPtr.Size == 8;
        }
    }

    struct PreAllocBuffer
    {
#if DEBUG
        PreAllocBufferManager Manager { get; set; }
#endif
        ArraySegment<Byte[]> Segment { get; set; }
    }
}
