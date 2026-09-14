using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Assert = UnityEngine.Assertions.Assert;

public class UnsafeCopy
{ 
    public unsafe static void MemCpy<SRC, DST>(SRC[] src, DST[] dst)
    where SRC : struct
    where DST : struct
    {
        int srcSize = src.Length * UnsafeUtility.SizeOf<SRC>();
        int dstSize = dst.Length * UnsafeUtility.SizeOf<DST>();
        Assert.AreEqual(srcSize, dstSize, $"{nameof(srcSize)}:{srcSize} and {nameof(dstSize)}:{dstSize} must be equal.");
        void* srcPtr = UnsafeUtility.PinGCArrayAndGetDataAddress(src, out ulong srcHandle);
        void* dstPtr = UnsafeUtility.PinGCArrayAndGetDataAddress(dst, out ulong dstHandle);
        UnsafeUtility.MemCpy(destination: dstPtr, source: srcPtr, size: srcSize);
        UnsafeUtility.ReleaseGCObject(srcHandle);
        UnsafeUtility.ReleaseGCObject(dstHandle);
    }

    public unsafe static void MemCpy<SRC, DST>(NativeArray<SRC> src, DST[] dst)
        where SRC : struct
        where DST : struct
    {
        int srcSize = src.Length * UnsafeUtility.SizeOf<SRC>();
        int dstSize = dst.Length * UnsafeUtility.SizeOf<DST>();
        Assert.AreEqual(srcSize, dstSize, $"{nameof(srcSize)}:{srcSize} and {nameof(dstSize)}:{dstSize} must be equal.");
        void* srcPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(src);
        void* dstPtr = UnsafeUtility.PinGCArrayAndGetDataAddress(dst, out ulong handle);
        UnsafeUtility.MemCpy(destination: dstPtr, source: srcPtr, size: srcSize);
        UnsafeUtility.ReleaseGCObject(handle);
    }

    public unsafe static void MemCpy<SRC, DST>(SRC[] src, NativeArray<DST> dst)
       where SRC : struct
       where DST : struct
    {
        int srcSize = src.Length * UnsafeUtility.SizeOf<SRC>();
        int dstSize = dst.Length * UnsafeUtility.SizeOf<DST>();
        Assert.AreEqual(srcSize, dstSize, $"{nameof(srcSize)}:{srcSize} and {nameof(dstSize)}:{dstSize} must be equal.");
        void* srcPtr = UnsafeUtility.PinGCArrayAndGetDataAddress(src, out ulong handle);
        void* dstPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(dst); 
        UnsafeUtility.MemCpy(destination: dstPtr, source: srcPtr, size: srcSize);
        UnsafeUtility.ReleaseGCObject(handle);
    }

}
