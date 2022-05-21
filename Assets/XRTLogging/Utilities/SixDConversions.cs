using System;
using UnityEngine;

namespace XRTLogging
{
    /// <summary>
    /// Helper static class that handles converting to and from the six dimensional representation of rotations.
    /// </summary>
    public static class SixDConversions{
        /// <summary>
        /// Interpret a float 4-ple as quaternion values and get 6D rotation representation.
        /// </summary>
        /// <param name="quatAsFloatTuple">a float 4-ple for the x,y,z,w values of a quaternion</param>
        /// <returns>float[6] of the 6 values. Order matters, do not shuffle these.</returns>
        public static float[] To6D((float x, float y, float z, float w) quatAsFloatTuple)
        {
            return To6D(new Quaternion(quatAsFloatTuple.x, quatAsFloatTuple.y, quatAsFloatTuple.z, quatAsFloatTuple.w));
        }

        /// <summary>
        /// Interpret a float 3-ple as euler values and get 6D rotation representation.
        /// </summary>
        /// <param name="eulerAsFloatTuple">a float 3-ple for x,y,z euler angles</param>
        /// <returns>float[6] of the 6 values. Order matters, do not shuffle these.</returns>
        public static float[] To6D((float x, float y, float z) eulerAsFloatTuple)
        {
            return To6D(new Vector3(eulerAsFloatTuple.x, eulerAsFloatTuple.y, eulerAsFloatTuple.z));
        }


        /// <summary>
        /// Get 6D-continuous representation of the provided Euler Angles.
        /// </summary>
        /// <param name="eulerAngles">Vector3 of euler angles</param>
        /// <returns>float[6] of the 6 values. Order matters, do not shuffle these.</returns>
        public static float[] To6D(Vector3 eulerAngles)
        {
            return To6D(Quaternion.Euler(eulerAngles));
        }

        /// <summary>
        /// Get 6D-continuous representation of the provided quaternion.
        /// </summary>
        /// <param name="quaternion">Quaternion of the rotation to be represented</param>
        /// <returns>float[6] of the 6 values. Order matters, do not shuffle these.</returns>
        public static float[] To6D(Quaternion quaternion)
        {
            return To6D(Matrix4x4.Rotate(quaternion));
        }

        /// <summary>
        /// Get the 6D-continuous representation of the provided quaternion, and put the values in-place.
        /// </summary>
        /// <param name="quaternion">Quaternion of the rotation to be represented</param>
        /// <param name="buffer">buffer of object[] to put the values into</param>
        /// <param name="index">start index to begin filling at</param>
        public static void CopyTo6DInPlace(Quaternion quaternion, ref object[] buffer,ref int index )
        {
            var mat = Matrix4x4.Rotate(quaternion);
            buffer[index++] = mat.m00; buffer[index++] = mat.m10; buffer[index++] = mat.m20;
            buffer[index++] = mat.m01; buffer[index++] = mat.m11; buffer[index++] = mat.m21;
        }

        /// <summary>
        /// Get 6D-continuous representation of the provided Mat4.
        /// </summary>
        /// <param name="matrix">Mat4x4 of the rotation to be represented</param>
        /// <returns>float[6] of the 6 values. Order matters, do not shuffle these.</returns>
        private static float[] To6D(Matrix4x4 matrix)
        {
            //NB: memory values go row-first (i.e. top row is m00,m01,m02,m03)
            //m00 = column0.x; m01 = column1.x; m02 = column2.x; m03 = column3.x;
            // these floats will be the two column vectors appended one after another
            return new[]
            {
                matrix.m00, matrix.m10, matrix.m20,
                matrix.m01, matrix.m11, matrix.m21
            };

            // (I'm adding a lot of comments here because this is a frequent source of error)
            // Basically here are where the values of M as they appear in the float[],
            // x indicates dropped values.

            /*
             * M = |0 3 x x| = |m00 m01 m02 m03|
             *     |1 4 x x|   |m10 m11 m12 m13|
             *     |2 5 x x|   |m20 m21 m22 m23|
             *     |x x x x|   |m30 m31 m32 m33|
             *
             * sixD = [m00, m10, m20, m01, m11, m21]
             */

        }

        /// <summary>
        /// Get the Mat4x4 form of the provided 6D-continuous representation of the rotation.
        /// </summary>
        /// <param name="sixD"></param>
        /// <returns>4x4 matrix representing the rotation.</returns>
        /// <exception cref="Exception">Throws an exception if given a float[] where length!=6</exception>
        public static Matrix4x4 From6D_Mat4(float[] sixD)
        {
            if (sixD.Length != 6)
            {
                throw new Exception(
                    $"Incorrect length of six D float[] representation. Length={sixD.Length} (must be 6 values).");
            }

            //a1 is first column vector3.
            var a1 = new Vector3(sixD[0], sixD[1], sixD[2]);
            var a2 = new Vector3(sixD[3], sixD[4], sixD[5]);
            var b1 = Vector3.Normalize(a1);
            var b2 = Vector3.Normalize(a2 - (Vector3.Dot(b1, a2) * b1));
            var b3 = Vector3.Cross(b1, b2);

            return new Matrix4x4(V34(b1), V34(b2), V34(b3), new Vector4(0f, 0f, 0f, 1f));
        }

        /// <summary>
        /// Get the Quaternion form of the provided 6D-continuous representation of the rotation.
        /// </summary>
        /// <param name="sixD">The 6D representation. (Order matters, do not shuffle these.)</param>
        /// <returns>Quaternion representing the rotation.</returns>
        public static Quaternion From6D_Quaternion(float[] sixD)
        {
            return From6D_Mat4(sixD).rotation;
        }

        /// <summary>
        /// Get the Euler Angle form of the provided 6D-continuous representation of the rotation.
        /// </summary>
        /// <param name="sixD">The 6D representation. (Order matters, do not shuffle these.)</param>
        /// <returns>Euler Angles (Vector3) representing the rotation.</returns>
        public static Vector3 From6D_Euler(float[] sixD)
        {
            return From6D_Quaternion(sixD).eulerAngles;

        }

        //Utility to make a V4 out of a V3.
        private static Vector4 V34(Vector3 v, float w = 0f)
        {
            return new Vector4(v.x, v.y, v.z, w);
        }

        //Utility to make a V3 out of a V4.
        private static Vector3 V43(Vector4 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }
    }
    
}
