using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace JobsUtils
{
    public static class JobsTypesConversions
    {
        public static Vector3[] Float4ArrayToVector3Array(float4[] inputArray)
        {
            Vector3[] outputArray = new Vector3[inputArray.Length];
            for (int i = 0; i < inputArray.Length; i++)
            {
                outputArray[i] = Float4ToVector3(inputArray[i]);
            }
            return outputArray;
        }

        public static Vector3[] Float3NativeArrayToVector3Array(NativeArray<float3> inputArray)
        {
            Vector3[] outputArray = new Vector3[inputArray.Length];
            for (int i = 0; i < inputArray.Length; i++)
            {
                outputArray[i] = Float3ToVector3(inputArray[i]);
            }
            return outputArray;
        }

        public static float4[] Vector3ArrayToFloat4Array(Vector3[] inputArray)
        {
            float4[] outputArray = new float4[inputArray.Length];
            for (int i = 0; i < inputArray.Length; i++)
            {
                outputArray[i] = Vector3ToFloat4(inputArray[i]);
            }
            return outputArray;
        }

        public static float4[] Vector3ArrayToFloat4Array(ref Vector3[] inputArray)
        {
            float4[] outputArray = new float4[inputArray.Length];
            for (int i = 0; i < inputArray.Length; i++)
            {
                outputArray[i] = Vector3ToFloat4(inputArray[i]);
            }
            return outputArray;
        }

        public static float3[] Vector3ArrayToFloat3Array(Vector3[] inputArray)
        {
            float3[] outputArray = new float3[inputArray.Length];
            for (int i = 0; i < inputArray.Length; i++)
            {
                outputArray[i] = Vector3ToFloat3(inputArray[i]);
            }
            return outputArray;
        }

        public static float4[] Vector2ArrayToFloat4Array(Vector2[] inputArray)
        {
            float4[] outputArray = new float4[inputArray.Length];
            for (int i = 0; i < inputArray.Length; i++)
            {
                outputArray[i] = Vector2ToFloat4(inputArray[i]);
            }
            return outputArray;
        }

        public static float4 Vector3ToFloat4(Vector3 v3)
        {
            return new float4(v3.x, v3.y, v3.z, 0);
        }

        public static float3 Vector3ToFloat3(Vector3 v3)
        {
            return new float3(v3.x, v3.y, v3.z);
        }

        public static float4 Vector2ToFloat4(Vector2 v2)
        {
            return new float4(v2.x, v2.y, 0, 0);
        }

        public static Vector3 Float4ToVector3(float4 f4)
        {
            return new Vector3(f4.x, f4.y, f4.z);
        }

        public static Vector3 Float3ToVector3(float3 f3)
        {
            return new Vector3(f3.x, f3.y, f3.z);
        }


        public static float4[] GetArrayFromNativeMap(NativeParallelHashMap<int, float4> map)
        {
            var size = map.Count();
            var res = new float4[size];
            for (int i = 0; i < size; i++)
            {
                res[i] = map[i];
            }
            return res;
        }
    }
    
}

