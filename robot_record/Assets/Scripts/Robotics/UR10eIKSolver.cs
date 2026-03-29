using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotMiddleware.Robotics
{
    /// <summary>
    /// Analytic inverse kinematics solver for the UR10e robot arm.
    /// Based on "Analytic Inverse Kinematics for the Universal Robots" by Hawkins (2013).
    /// Returns up to 8 solutions for a given end-effector pose.
    /// </summary>
    public static class UR10eIKSolver
    {
        // UR10e DH Parameters (Modified DH convention)
        // Source: Universal Robots UR10e datasheet
        private static readonly double d1 = 0.1807;
        private static readonly double a2 = -0.6127;
        private static readonly double a3 = -0.57155;
        private static readonly double d4 = 0.17415;
        private static readonly double d5 = 0.11985;
        private static readonly double d6 = 0.11655;

        // Joint limits (radians) — UR10e allows full rotation
        private static readonly double JointMin = -2.0 * Math.PI;
        private static readonly double JointMax = 2.0 * Math.PI;

        private const double ZERO_THRESH = 1e-8;

        /// <summary>
        /// Compute up to 8 IK solutions for a target end-effector pose.
        /// </summary>
        /// <param name="targetPos">Target position in robot base frame (meters)</param>
        /// <param name="targetRot">Target orientation in robot base frame</param>
        /// <returns>Array of solutions, each containing 6 joint angles (radians)</returns>
        public static float[][] SolveIK(Vector3 targetPos, Quaternion targetRot)
        {
            // Build 4x4 homogeneous transform from position + rotation
            double[,] T = QuaternionToMatrix(targetPos, targetRot);
            return SolveIK(T);
        }

        /// <summary>
        /// Compute IK from a 4x4 homogeneous transformation matrix.
        /// </summary>
        public static float[][] SolveIK(double[,] T)
        {
            var solutions = new List<float[]>();

            double nx = T[0, 0], ox = T[0, 1], ax = T[0, 2], px = T[0, 3];
            double ny = T[1, 0], oy = T[1, 1], ay = T[1, 2], py = T[1, 3];
            double nz = T[2, 0], oz = T[2, 1], az = T[2, 2], pz = T[2, 3];

            // Wrist center position (subtract d6 along approach vector)
            double wcx = px - d6 * ax;
            double wcy = py - d6 * ay;
            double wcz = pz - d6 * az;

            // === Solve theta1 (2 solutions) ===
            double[] theta1 = new double[2];
            double r = Math.Sqrt(wcx * wcx + wcy * wcy);

            if (Math.Abs(r) < ZERO_THRESH)
            {
                // Singularity: wrist center on z-axis
                theta1[0] = 0;
                theta1[1] = Math.PI;
            }
            else
            {
                double phi = Math.Atan2(wcy, wcx);
                double acos_arg = Clamp(d4 / r, -1.0, 1.0);
                double alpha = Math.Acos(acos_arg);
                theta1[0] = phi + alpha + Math.PI / 2.0;
                theta1[1] = phi - alpha + Math.PI / 2.0;
            }

            for (int i1 = 0; i1 < 2; i1++)
            {
                double t1 = theta1[i1];
                double s1 = Math.Sin(t1);
                double c1 = Math.Cos(t1);

                // === Solve theta5 (2 solutions for each theta1) ===
                double acos5_arg = Clamp((px * s1 - py * c1 - d4) / d6, -1.0, 1.0);

                double[] theta5 = new double[2];
                theta5[0] = Math.Acos(acos5_arg);
                theta5[1] = -Math.Acos(acos5_arg);

                for (int i5 = 0; i5 < 2; i5++)
                {
                    double t5 = theta5[i5];
                    double s5 = Math.Sin(t5);
                    double c5 = Math.Cos(t5);

                    // === Solve theta6 ===
                    double t6;
                    if (Math.Abs(s5) < ZERO_THRESH)
                    {
                        t6 = 0; // Singularity
                    }
                    else
                    {
                        double num = (-oy * s1 + ox * c1);
                        double den = (ny * s1 - nx * c1);
                        t6 = Math.Atan2(num / s5, den / s5);
                    }

                    // === Solve theta2, theta3 (2 solutions) ===
                    double c6 = Math.Cos(t6);
                    double s6 = Math.Sin(t6);

                    // Compute the position of wrist 1 center
                    double m = d5 * (s6 * (nx * c1 + ny * s1) + c6 * (ox * c1 + oy * s1))
                             - d6 * (ax * c1 + ay * s1) + px * c1 + py * s1;
                    double n = pz - d1 - d6 * az
                             + d5 * (nz * s6 + oz * c6);

                    // 2R planar arm: solve for theta2, theta3
                    double c3_arg = (m * m + n * n - a2 * a2 - a3 * a3) / (2.0 * a2 * a3);
                    c3_arg = Clamp(c3_arg, -1.0, 1.0);

                    double[] theta3 = new double[2];
                    theta3[0] = Math.Acos(c3_arg);
                    theta3[1] = -Math.Acos(c3_arg);

                    for (int i3 = 0; i3 < 2; i3++)
                    {
                        double t3 = theta3[i3];
                        double s3 = Math.Sin(t3);
                        double c3 = Math.Cos(t3);

                        double det = a2 * a2 + a3 * a3 + 2.0 * a2 * a3 * c3;
                        if (Math.Abs(det) < ZERO_THRESH) continue;

                        double s2 = (n * (a2 + a3 * c3) - m * a3 * s3) / det;
                        double c2 = (m * (a2 + a3 * c3) + n * a3 * s3) / det;
                        double t2 = Math.Atan2(s2, c2);

                        // === Solve theta4 ===
                        double[,] T01 = DHMatrix(0, d1, 0, Math.PI / 2.0, t1);
                        double[,] T12 = DHMatrix(a2, 0, 0, 0, t2);
                        double[,] T23 = DHMatrix(a3, 0, 0, 0, t3);
                        double[,] T03 = Multiply(Multiply(T01, T12), T23);
                        double[,] T03_inv = InvertHomogeneous(T03);
                        double[,] T36 = Multiply(T03_inv, T);

                        double t4 = Math.Atan2(T36[1, 0], T36[0, 0]);

                        // Store solution
                        float[] sol = new float[6];
                        sol[0] = (float)NormalizeAngle(t1);
                        sol[1] = (float)NormalizeAngle(t2);
                        sol[2] = (float)NormalizeAngle(t3);
                        sol[3] = (float)NormalizeAngle(t4);
                        sol[4] = (float)NormalizeAngle(t5);
                        sol[5] = (float)NormalizeAngle(t6);

                        if (ValidateSolution(sol))
                            solutions.Add(sol);
                    }
                }
            }

            return solutions.ToArray();
        }

        /// <summary>
        /// Select the IK solution closest to the current joint configuration.
        /// </summary>
        public static float[] SelectBestSolution(float[][] solutions, float[] currentJoints)
        {
            if (solutions == null || solutions.Length == 0) return null;
            if (currentJoints == null) return solutions[0];

            float bestCost = float.MaxValue;
            float[] bestSolution = solutions[0];

            foreach (var sol in solutions)
            {
                float cost = 0;
                for (int i = 0; i < 6; i++)
                {
                    float diff = Mathf.Abs(sol[i] - currentJoints[i]);
                    // Wrap around for angular distance
                    if (diff > Mathf.PI) diff = 2f * Mathf.PI - diff;
                    cost += diff;
                }

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestSolution = sol;
                }
            }

            return bestSolution;
        }

        /// <summary>
        /// Compute forward kinematics: joint angles → end-effector 4x4 transform.
        /// </summary>
        public static Matrix4x4 ForwardKinematics(float[] joints)
        {
            double[,] T01 = DHMatrix(0, d1, 0, Math.PI / 2.0, joints[0]);
            double[,] T12 = DHMatrix(a2, 0, 0, 0, joints[1]);
            double[,] T23 = DHMatrix(a3, 0, 0, 0, joints[2]);
            double[,] T34 = DHMatrix(0, d4, 0, Math.PI / 2.0, joints[3]);
            double[,] T45 = DHMatrix(0, d5, 0, -Math.PI / 2.0, joints[4]);
            double[,] T56 = DHMatrix(0, d6, 0, 0, joints[5]);

            double[,] T = Multiply(Multiply(Multiply(Multiply(Multiply(T01, T12), T23), T34), T45), T56);

            return ToMatrix4x4(T);
        }

        /// <summary>
        /// Check if a solution is within joint limits.
        /// </summary>
        public static bool ValidateSolution(float[] solution)
        {
            if (solution == null || solution.Length != 6) return false;
            for (int i = 0; i < 6; i++)
            {
                if (float.IsNaN(solution[i]) || float.IsInfinity(solution[i]))
                    return false;
                if (solution[i] < JointMin || solution[i] > JointMax)
                    return false;
            }
            return true;
        }

        // ═══ DH Matrix Construction ═══

        private static double[,] DHMatrix(double a, double d, double alpha_offset, double alpha, double theta)
        {
            double ct = Math.Cos(theta);
            double st = Math.Sin(theta);
            double ca = Math.Cos(alpha);
            double sa = Math.Sin(alpha);

            return new double[,] {
                { ct, -st * ca,  st * sa, a * ct },
                { st,  ct * ca, -ct * sa, a * st },
                { 0,   sa,       ca,      d      },
                { 0,   0,        0,       1      }
            };
        }

        // ═══ Matrix Utilities ═══

        private static double[,] Multiply(double[,] A, double[,] B)
        {
            double[,] C = new double[4, 4];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    for (int k = 0; k < 4; k++)
                        C[i, j] += A[i, k] * B[k, j];
            return C;
        }

        private static double[,] InvertHomogeneous(double[,] T)
        {
            double[,] inv = new double[4, 4];
            // Rotation transpose
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    inv[i, j] = T[j, i];
            // Translation: -R^T * t
            for (int i = 0; i < 3; i++)
                inv[i, 3] = -(inv[i, 0] * T[0, 3] + inv[i, 1] * T[1, 3] + inv[i, 2] * T[2, 3]);
            inv[3, 3] = 1.0;
            return inv;
        }

        private static double[,] QuaternionToMatrix(Vector3 pos, Quaternion rot)
        {
            Matrix4x4 m = Matrix4x4.TRS(pos, rot, Vector3.one);
            double[,] T = new double[4, 4];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    T[i, j] = m[i, j]; // Unity Matrix4x4 is column-major but indexer is [row,col]
            return T;
        }

        private static Matrix4x4 ToMatrix4x4(double[,] T)
        {
            Matrix4x4 m = Matrix4x4.identity;
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    m[i, j] = (float)T[i, j];
            return m;
        }

        private static double NormalizeAngle(double angle)
        {
            while (angle > Math.PI) angle -= 2.0 * Math.PI;
            while (angle < -Math.PI) angle += 2.0 * Math.PI;
            return angle;
        }

        private static double Clamp(double val, double min, double max)
        {
            return Math.Max(min, Math.Min(max, val));
        }
    }
}
