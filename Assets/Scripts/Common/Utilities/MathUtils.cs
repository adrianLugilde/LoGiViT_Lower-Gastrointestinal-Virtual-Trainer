using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Utilities {

   
    public static class MathUtils
    {
        /// <summary>
        /// Computes the distance from a given point to a plane
        /// </summary>
        /// <param name="planeNormal"></param>
        /// <param name="planePos"></param>
        /// <param name="pointPos"></param>
        /// <returns></returns>
        public static float DistanceFromPointToPlane(Vector3 planeNormal, Vector3 planePos, Vector3 pointPos)
        {
            //Positive distance denotes that the point p is on the front side of the plane 
            //Negative means it's on the back side
            float distance = Vector3.Dot(planeNormal, pointPos - planePos);

            return distance;
        }

        /// <summary>
        /// Returns the centroid given a collection of Vector2
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Vector2 GetCentroid(IEnumerable<Vector2> points)
        {
            var centroid = new Vector2();
            foreach (var point in points)
            {
                centroid += point / points.Count();
            }
            return centroid;
        }

        /// <summary>
        /// Returns the centroid given a collection of Vector3
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Vector3 GetCentroid(IEnumerable<Vector3> points)
        {
            var centroid = Vector3.zero;
            foreach (var point in points)
            {
                centroid += point / points.Count();
            }
            return centroid;
        }


        public static Vector3 Plane3Intersect(Plane p1, Plane p2, Plane p3)
        {
            return ((-p1.distance * Vector3.Cross(p2.normal, p3.normal)) +
                    (-p2.distance * Vector3.Cross(p3.normal, p1.normal)) +
                    (-p3.distance * Vector3.Cross(p1.normal, p2.normal))) /
                (Vector3.Dot(p1.normal, Vector3.Cross(p2.normal, p3.normal)));
        }


        public static bool IsInRange(float value, float minValue, float maxValue, bool inclusive = true)
        {
            var res = (value - minValue) * (maxValue - value);
            return inclusive ? res >= 0 : res > 0;
        }

        public static bool IsInRange(int value, int minValue, int maxValue, bool inclusive = true)
        {
            var res = (value - minValue) * (maxValue - value);
            return inclusive ? res >= 0 : res > 0;
        }
    }
}

