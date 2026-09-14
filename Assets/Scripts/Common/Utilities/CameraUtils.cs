using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.Mathematics;
using UnityEngine;
using Utilities;

public static class CameraUtils
{
    public static void DrawFustrum(ref Camera camera, ref List<LineRenderer> fustrumLineRenderers, Transform originTransform, float lineRenderesCornerDistance)
    {
        /*Vector3[] farCorners = new Vector3[4]; //Approx'd farplane corners
        Plane[] camPlanes = GeometryUtility.CalculateFrustumPlanes(camera); //get planes from matrix
        Plane temp = camPlanes[1]; camPlanes[1] = camPlanes[2]; camPlanes[2] = temp; //swap [1] and [2] so the order is better for the loop
        for (int i = 0; i < 4; i++)
        {
            //nearCorners[i] = Plane3Intersect(camPlanes[4], camPlanes[i], camPlanes[(i + 1) % 4]); //near corners on the created projection matrix
            farCorners[i] = MathUtils.Plane3Intersect(camPlanes[5], camPlanes[i], camPlanes[(i + 1) % 4]); //far corners on the created projection matrix
            farCorners[i] = Vector3.MoveTowards(camera.transform.position, originTransform.InverseTransformPoint(farCorners[i]) + originTransform.position, lineRendererScale);
        }


        int lineRendererIdx = 0;

        for (int i = 0; i < 4; i++)
        {
            fustrumLineRenderers[lineRendererIdx].SetPosition(0, camera.transform.position);
            fustrumLineRenderers[lineRendererIdx++].SetPosition(1, farCorners[i]);
            fustrumLineRenderers[lineRendererIdx].SetPosition(0, farCorners[i]);
            fustrumLineRenderers[lineRendererIdx++].SetPosition(1, farCorners[(i + 1) % 4]);
        }*/
        // 1) Get frustum corners in CAMERA LOCAL space
        var corners = new Vector3[4];
        var eye = camera.stereoEnabled ? Camera.MonoOrStereoscopicEye.Mono : Camera.MonoOrStereoscopicEye.Mono;
        camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), lineRenderesCornerDistance, eye, corners);

        // 2) Convert to WORLD space
        for (int i = 0; i < 4; i++)
            corners[i] = camera.transform.TransformPoint(corners[i]);

        // 3) Make sure line renderers expect WORLD positions
        foreach (var lr in fustrumLineRenderers) lr.useWorldSpace = true;

        int k = 0;
        for (int i = 0; i < 4; i++)
        {
            // from camera to each far corner
            fustrumLineRenderers[k].SetPosition(0, camera.transform.position);
            fustrumLineRenderers[k++].SetPosition(1, corners[i]);

            // around the far rectangle
            fustrumLineRenderers[k].SetPosition(0, corners[i]);
            fustrumLineRenderers[k++].SetPosition(1, corners[(i + 1) % 4]);
        }
    }

    public static bool IsPointInViewport(ref Camera camera, Vector3 position, out Vector3 viewport)
    {
        viewport = camera.WorldToViewportPoint(position);
        return viewport.z > 0 && viewport.x > 0 && viewport.y > 0 && viewport.x < 1 && viewport.y < 1;
    }

    public static bool IsPointInFustrum(ref Camera camera, Vector3 point)
    {
        Plane[] camPlanes = GeometryUtility.CalculateFrustumPlanes(camera); //get planes from matrix
        int planeCount = camPlanes.Length;
        for (int i = 0; i < planeCount; i++)
        {
            var plane = camPlanes[i];
            if (plane.GetDistanceToPoint(point) < 0) return false;
        }
        return false;
    }

    public static bool IsPointOnScreenAlternative(ref Camera camera, Vector3 position, float renderWidth, float renderHeight)
    {
        Matrix4x4 V = camera.worldToCameraMatrix;
        Matrix4x4 P = camera.projectionMatrix;
        float4x4 MVP = math.mul(P, V);
        float4 screenPos = math.mul(MVP, new float4(position.x, position.y, position.z, 1));
        //Vector3 screenPos = MVP.MultiplyPoint(position);
        Vector3 onScreen = new Vector3(screenPos.x + 1f, screenPos.y + 1f, screenPos.z + 1f) / 2f;
        return onScreen.z > 0 && onScreen.x > 0 && onScreen.y > 0 && onScreen.x < 1 && onScreen.y < 1;
    }

    public static Vector3 WorldToScreen(Matrix4x4 camMVP, Vector3 point, float renderWidth, float renderHeight)
    {
        Vector3 result;
        result.x = camMVP.m00 * point.x + camMVP.m01 * point.y + camMVP.m02 * point.z + camMVP.m03;
        result.y = camMVP.m10 * point.x + camMVP.m11 * point.y + camMVP.m12 * point.z + camMVP.m13;
        result.z = camMVP.m20 * point.x + camMVP.m21 * point.y + camMVP.m22 * point.z + camMVP.m23;
        float num = camMVP.m30 * point.x + camMVP.m31 * point.y + camMVP.m32 * point.z + camMVP.m33;
        num = 1f / num;
        result.x *= num;
        result.y *= num;
        result.z = num; // num contains the 1 / (distance to camera), ideal to linearly interpolate the depth in a rasterizer
        point = result;

        point.x = (point.x * 0.5f + 0.5f) * renderWidth;
        point.y = (point.y * 0.5f + 0.5f) * renderHeight;

        return point;
    }
}