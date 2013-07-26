using System;
using System.Diagnostics;

namespace Myriax.Eonfusion.API.Helpers
{

    public struct VTransform1D
    {

        #region Constructors.

        public VTransform1D(VTransform1D value)
        {
            m_element = new double[2 * 2];
            for (int i = 0; i < 2 * 2; ++i)
                m_element[i] = value.m_element[i];
        }

        public VTransform1D(VTransformND value)
        {
            if (value.Dimension != 1) {
                throw new ArgumentException("Inappropriate dimension.", "value");
            }

            m_element = (double[])value.GetElements().Clone();
        }

        public VTransform1D(
            double m00, double m01,
            double m10, double m11)
        {
            m_element = new double[2 * 2];

            m_element[0 * 2 + 0] = m00;
            m_element[1 * 2 + 0] = m01;

            m_element[0 * 2 + 1] = m10;
            m_element[1 * 2 + 1] = m11;
        }

        #endregion

        #region Public access methods.

        public void SetElement(int row, int col, double value)
        {
            Debug.Assert(row >= 0 && row <= 1);
            Debug.Assert(col >= 0 && col <= 1);
            Init();
            m_element[col * 2 + row] = value;
        }

        public VTransform1D ChangeElement(int row, int col, double value)
        {
            Debug.Assert(row >= 0 && row <= 1);
            Debug.Assert(col >= 0 && col <= 1);
            Init();

            VTransform1D result = new VTransform1D();
            result.Init();
            for (int i = 0; i < 2 * 2; ++i) {
                result.m_element[i] = m_element[i];
            }
            result.m_element[col * 2 + row] = value;

            return result;
        }

        public static bool operator ==(VTransform1D valueL, VTransform1D valueR)
        {
            valueL.Init();
            valueR.Init();

            for (int i = 0; i < 2 * 2; ++i)
                if (valueL.m_element[i] != valueR.m_element[i])
                    return false;

            return true;
        }

        public static bool operator !=(VTransform1D valueL, VTransform1D valueR)
        {
            valueL.Init();
            valueR.Init();

            for (int i = 0; i < 2 * 2; ++i)
                if (valueL.m_element[i] != valueR.m_element[i])
                    return true;

            return false;
        }

        public bool Equals(VTransform1D transform)
        {
            return this == transform;
        }

        public override bool Equals(object transform)
        {
            return (transform is VTransform1D) && this == (VTransform1D)transform;
        }

        public override int GetHashCode()
        {
            Init();
            return base.GetHashCode();
        }

        public static VTransform1D Transpose(VTransform1D t)
        {
            t.Init();
            VTransform1D result = new VTransform1D();
            result.Init();
            result.m_element[0] = t.m_element[0];
            result.m_element[1] = t.m_element[2];
            result.m_element[2] = t.m_element[1];
            result.m_element[3] = t.m_element[3];
            return result;
        }

        public static VTransform1D operator *(VTransform1D valueL, VTransform1D valueR)
        {
            valueL.Init();
            valueR.Init();

            VTransform1D result = new VTransform1D();
            result.Init();
            result.m_element[0] = valueL.m_element[0] * valueR.m_element[0] + valueL.m_element[2] * valueR.m_element[1];
            result.m_element[1] = valueL.m_element[1] * valueR.m_element[0] + valueL.m_element[2] * valueR.m_element[1];
            result.m_element[2] = valueL.m_element[0] * valueR.m_element[2] + valueL.m_element[3] * valueR.m_element[3];
            result.m_element[3] = valueL.m_element[1] * valueR.m_element[2] + valueL.m_element[3] * valueR.m_element[3];
            return result;
        }

        public static VCoordinate1D operator *(VTransform1D transform, VCoordinate1D coord)
        {
            transform.Init();

            VCoordinate1D result = new VCoordinate1D();
            result.X = transform.m_element[0] * coord[0] + transform.m_element[2];
            double y = transform.m_element[1] * coord[0] + transform.m_element[3];
            Debug.Assert(y != 0.0);
            y = 1.0 / y;
            result.X *= y;
            return result;
        }

        public static VCoordinate2D operator *(VTransform1D transform, VCoordinate2D coord)
        {
            transform.Init();

            VCoordinate2D result = new VCoordinate2D();
            result.X = transform.m_element[0] * coord[0] + transform.m_element[2] * coord[1];
            result.Y = transform.m_element[1] * coord[0] + transform.m_element[3] * coord[1];
            return result;
        }

        public static VTransform1D Identity()
        {
            VTransform1D result = new VTransform1D();
            result.Init();
            result.m_element[0] = 1.0;
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 1.0;
            return result;
        }

        public static VTransform1D Translation(ICoordinate translation)
        {
            Debug.Assert(translation.Dimension == 1);
            VTransform1D result = new VTransform1D();
            result.Init();
            result.m_element[0] = 1.0;
            result.m_element[1] = 0.0;
            result.m_element[2] = translation[0];
            result.m_element[3] = 1.0;
            return result;
        }

        public static VTransform1D Scale(ICoordinate scale)
        {
            Debug.Assert(scale.Dimension == 1);
            VTransform1D result = new VTransform1D();
            result.Init();
            result.m_element[0] = scale[0];
            result.m_element[1] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 1.0;
            return result;
        }

        public static VTransform1D Projection(double projectionX)
        {
            VTransform1D result = new VTransform1D();
            result.Init();
            result.m_element[0] = 1.0;
            result.m_element[1] = 1.0 / projectionX;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            return result;
        }

        #endregion

        #region Private methods.

        private void Init()
        {
            if (m_element == null)
                m_element = new double[2 * 2];
        }

        #endregion

        #region Private fields.

        private double[] m_element;

        #endregion

    }



    public struct VTransform2D
    {

        #region Constructors.

        public VTransform2D(VTransform2D value)
        {
            m_element = new double[3 * 3];
            for (int i = 0; i < 3 * 3; ++i)
                m_element[i] = value.m_element[i];
        }

        public VTransform2D(VTransformND value)
        {
            if (value.Dimension != 2) {
                throw new ArgumentException("Inappropriate dimension.", "value");
            }

            m_element = (double[])value.GetElements().Clone();
        }

        public VTransform2D(
            double m00, double m01, double m02,
            double m10, double m11, double m12,
            double m20, double m21, double m22)
        {
            m_element = new double[3 * 3];

            m_element[0 * 3 + 0] = m00;
            m_element[1 * 3 + 0] = m01;
            m_element[2 * 3 + 0] = m02;

            m_element[0 * 3 + 1] = m10;
            m_element[1 * 3 + 1] = m11;
            m_element[2 * 3 + 1] = m12;

            m_element[0 * 3 + 2] = m20;
            m_element[1 * 3 + 2] = m21;
            m_element[2 * 3 + 2] = m22;
        }

        #endregion

        #region Public access methods.

        public double GetElement(int row, int col)
        {
            Debug.Assert(row >= 0 && row <= 2);
            Debug.Assert(col >= 0 && col <= 2);
            Init();
            return m_element[col * 3 + row];
        }

        public VTransform2D ChangeElement(int row, int col, double value)
        {
            Debug.Assert(row >= 0 && row <= 2);
            Debug.Assert(col >= 0 && col <= 2);
            Init();

            VTransform2D result = new VTransform2D();
            result.Init();
            for (int i = 0; i < 3 * 3; ++i) {
                result.m_element[i] = m_element[i];
            }
            result.m_element[col * 3 + row] = value;

            return result;
        }

        public static bool operator ==(VTransform2D valueL, VTransform2D valueR)
        {
            valueL.Init();
            valueR.Init();

            for (int i = 0; i < 3 * 3; ++i)
                if (valueL.m_element[i] != valueR.m_element[i])
                    return false;

            return true;
        }

        public static bool operator !=(VTransform2D valueL, VTransform2D valueR)
        {
            valueL.Init();
            valueR.Init();

            for (int i = 0; i < 3 * 3; ++i)
                if (valueL.m_element[i] != valueR.m_element[i])
                    return true;

            return false;
        }

        public bool Equals(VTransform2D transform)
        {
            return this == transform;
        }

        public override bool Equals(object transform)
        {
            return (transform is VTransform2D) && this == (VTransform2D)transform;
        }

        public override int GetHashCode()
        {
            Init();
            return base.GetHashCode();
        }

        public static VTransform2D Transpose(VTransform2D t)
        {
            t.Init();
            VTransform2D result = new VTransform2D();
            result.Init();
            result.m_element[0] = t.m_element[0];
            result.m_element[1] = t.m_element[3];
            result.m_element[2] = t.m_element[6];
            result.m_element[3] = t.m_element[1];
            result.m_element[4] = t.m_element[4];
            result.m_element[5] = t.m_element[7];
            result.m_element[6] = t.m_element[2];
            result.m_element[7] = t.m_element[5];
            result.m_element[8] = t.m_element[8];

            return result;
        }

        public static VTransform2D operator *(VTransform2D valueL, VTransform2D valueR)
        {
            valueL.Init();
            valueR.Init();
            VTransform2D result = new VTransform2D();
            result.Init();
            result.m_element[0] = valueL.m_element[0] * valueR.m_element[0] + valueL.m_element[3] * valueR.m_element[1] + valueL.m_element[6] * valueR.m_element[2];
            result.m_element[1] = valueL.m_element[1] * valueR.m_element[0] + valueL.m_element[4] * valueR.m_element[1] + valueL.m_element[7] * valueR.m_element[2];
            result.m_element[2] = valueL.m_element[2] * valueR.m_element[0] + valueL.m_element[5] * valueR.m_element[1] + valueL.m_element[8] * valueR.m_element[2];
            result.m_element[3] = valueL.m_element[0] * valueR.m_element[3] + valueL.m_element[3] * valueR.m_element[4] + valueL.m_element[6] * valueR.m_element[5];
            result.m_element[4] = valueL.m_element[1] * valueR.m_element[3] + valueL.m_element[4] * valueR.m_element[4] + valueL.m_element[7] * valueR.m_element[5];
            result.m_element[5] = valueL.m_element[2] * valueR.m_element[3] + valueL.m_element[5] * valueR.m_element[4] + valueL.m_element[8] * valueR.m_element[5];
            result.m_element[6] = valueL.m_element[0] * valueR.m_element[6] + valueL.m_element[3] * valueR.m_element[7] + valueL.m_element[6] * valueR.m_element[8];
            result.m_element[7] = valueL.m_element[1] * valueR.m_element[6] + valueL.m_element[4] * valueR.m_element[7] + valueL.m_element[7] * valueR.m_element[8];
            result.m_element[8] = valueL.m_element[2] * valueR.m_element[6] + valueL.m_element[5] * valueR.m_element[7] + valueL.m_element[8] * valueR.m_element[8];
            return result;
        }

        /// <summary>
        /// Return the inverse of the matrix.
        /// </summary>
        /// <remarks>
        /// Uses Gauss-Jordan matrix inversion algorithm.
        /// See http://www.cs.berkeley.edu/~wkahan/MathH110/gji.pdf
        /// </remarks>
        /// <param name="t">Input matrix.</param>
        /// <returns>Inverse of t.</returns>
        public static VTransform2D Inverse(VTransform2D t)
        {
            t.Init();
            VTransform2D result = new VTransform2D(t);
            result.Init();

            double UFL = 5.9E-39;
            double G = 4.0;
            G = G / 3.0;
            G = G - 1.0;
            double EPS = Math.Abs(((G + G) - 1.0) + G);

            double[] maxColVal = new double[3];
            int[] colOrder = new int[3];

            for (int col = 0; col < 3; ++col) {
                maxColVal[col] = 0.0;
                for (int row = 0; row < 3; ++row) {
                    double x = Math.Abs(result.m_element[3 * row + col]);
                    if (x > maxColVal[col])
                        maxColVal[col] = x;
                }
            }

            for (int step = 0; step < 3; ++step) {
                //  Perform elimination upon column k.
                double pivotVal = 0.0;
                int pivotRow = step;
                for (int row = step; row < 3; ++row) {
                    double x = Math.Abs(result.m_element[3 * row + step]);
                    if (x > pivotVal) {
                        pivotVal = x;
                        pivotRow = row;
                    }
                }

                if (pivotVal == 0.0) {
                    pivotVal = EPS * maxColVal[step] + UFL;
                    result.m_element[3 * step + step] = pivotVal;
                }

                if (maxColVal[step] > 0.0) {
                    pivotVal = pivotVal / maxColVal[step];
                    if (pivotVal > G)
                        G = pivotVal;
                }

                colOrder[step] = pivotRow;
                if (pivotRow != step) {
                    for (int col = 0; col < 3; ++col) {
                        double temp = result.m_element[3 * pivotRow + col];
                        result.m_element[3 * pivotRow + col] = result.m_element[3 * step + col];
                        result.m_element[3 * step + col] = temp;
                    }
                }

                pivotVal = result.m_element[3 * step + step];
                result.m_element[3 * step + step] = 1.0;
                for (pivotRow = 0; pivotRow < 3; ++pivotRow)
                    result.m_element[3 * step + pivotRow] /= pivotVal;

                for (int row = 0; row < 3; ++row) {
                    if (row == step)
                        continue;

                    double temp = result.m_element[3 * row + step];
                    result.m_element[3 * row + step] = 0.0;
                    for (int col = 0; col < 3; ++col)
                        result.m_element[3 * row + col] -= result.m_element[3 * step + col] * temp;
                }
            }

            //  Unswap columns.
            for (int col = 2; col >= 0; --col) {
                int newCol = colOrder[col];
                if (newCol == col)
                    continue;

                for (int row = 0; row < 3; ++row) {
                    double temp = result.m_element[3 * row + col];
                    result.m_element[3 * row + col] = result.m_element[3 * row + newCol];
                    result.m_element[3 * row + newCol] = temp;
                }
            }

            //VTransform4D test = t.MultiplyBy(result);

            return result;
        }

        public VTransform2D MultiplyBy(VTransform2D rhs)
        {
            Init();
            rhs.Init();
            VTransform2D result = new VTransform2D();
            result.Init();
            result.m_element[0] = m_element[0] * rhs.m_element[0] + m_element[3] * rhs.m_element[1] + m_element[6] * rhs.m_element[2];
            result.m_element[1] = m_element[1] * rhs.m_element[0] + m_element[4] * rhs.m_element[1] + m_element[7] * rhs.m_element[2];
            result.m_element[2] = m_element[2] * rhs.m_element[0] + m_element[5] * rhs.m_element[1] + m_element[8] * rhs.m_element[2];
            result.m_element[3] = m_element[0] * rhs.m_element[3] + m_element[3] * rhs.m_element[4] + m_element[6] * rhs.m_element[5];
            result.m_element[4] = m_element[1] * rhs.m_element[3] + m_element[4] * rhs.m_element[4] + m_element[7] * rhs.m_element[5];
            result.m_element[5] = m_element[2] * rhs.m_element[3] + m_element[5] * rhs.m_element[4] + m_element[8] * rhs.m_element[5];
            result.m_element[6] = m_element[0] * rhs.m_element[6] + m_element[3] * rhs.m_element[7] + m_element[6] * rhs.m_element[8];
            result.m_element[7] = m_element[1] * rhs.m_element[6] + m_element[4] * rhs.m_element[7] + m_element[7] * rhs.m_element[8];
            result.m_element[8] = m_element[2] * rhs.m_element[6] + m_element[5] * rhs.m_element[7] + m_element[8] * rhs.m_element[8];
            return result;
        }

        public static VCoordinate2D operator *(VTransform2D transform, VCoordinate2D coord)
        {
            transform.Init();

            VCoordinate2D result = new VCoordinate2D();
            result.X = transform.m_element[0] * coord[0] + transform.m_element[3] * coord[1] + transform.m_element[6];
            result.Y = transform.m_element[1] * coord[0] + transform.m_element[4] * coord[1] + transform.m_element[7];
            double z = transform.m_element[2] * coord[0] + transform.m_element[5] * coord[1] + transform.m_element[8];
            Debug.Assert(z != 0.0);
            z = 1.0 / z;
            result.X *= z;
            result.Y *= z;
            return result;
        }

        public static VCoordinate3D operator *(VTransform2D transform, VCoordinate3D coord)
        {
            transform.Init();

            VCoordinate3D result = new VCoordinate3D();
            result.X = transform.m_element[0] * coord[0] + transform.m_element[3] * coord[1] + transform.m_element[6] * coord[2];
            result.Y = transform.m_element[1] * coord[0] + transform.m_element[4] * coord[1] + transform.m_element[7] * coord[2];
            result.Z = transform.m_element[2] * coord[0] + transform.m_element[5] * coord[1] + transform.m_element[8] * coord[2];
            return result;
        }

        public static VTransform2D Identity()
        {
            VTransform2D result = new VTransform2D();
            result.Init();
            result.m_element[0] = 1.0;
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 1.0;
            result.m_element[5] = 0.0;
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 1.0;
            return result;
        }

        public static VTransform2D Translation(ICoordinate translation)
        {
            Debug.Assert(translation.Dimension == 2);
            VTransform2D result = new VTransform2D();
            result.Init();
            result.m_element[0] = 1.0;
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 1.0;
            result.m_element[5] = 0.0;
            result.m_element[6] = translation[0];
            result.m_element[7] = translation[1];
            result.m_element[8] = 1.0;
            return result;
        }

        public static VTransform2D Scale(ICoordinate scale)
        {
            Debug.Assert(scale.Dimension == 2);
            VTransform2D result = new VTransform2D();
            result.Init();
            result.m_element[0] = scale[0];
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = scale[1];
            result.m_element[5] = 0.0;
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 1.0;
            return result;
        }

        public static VTransform2D Rotation(double angle)
        {
            VTransform2D result = new VTransform2D();
            result.Init();
            double cosAngle = Math.Cos(angle);
            double sinAngle = Math.Sin(angle);
            result.m_element[0] = cosAngle;
            result.m_element[1] = sinAngle;
            result.m_element[2] = 0.0;
            result.m_element[3] = -sinAngle;
            result.m_element[4] = cosAngle;
            result.m_element[5] = 0.0;
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 1.0;
            return result;
        }

        public static VTransform2D Projection(double projectionY)
        {
            VTransform2D result = new VTransform2D();
            result.Init();
            result.m_element[0] = 1.0;
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 1.0;
            result.m_element[5] = 1.0 / projectionY;
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 0.0;
            return result;
        }

        #endregion

        #region Private methods.

        private void Init()
        {
            if (m_element == null)
                m_element = new double[3 * 3];
        }

        #endregion

        #region Private fields.

        private double[] m_element;

        #endregion

    }



    public struct VTransform3D
    {

        #region Constructors.

        public VTransform3D(VTransform3D value)
        {
            m_element = new double[4 * 4];
            for (int i = 0; i < 4 * 4; ++i)
                m_element[i] = value.m_element[i];
        }

        public VTransform3D(VTransformND value)
        {
            if (value.Dimension != 3) {
                throw new ArgumentException("Inappropriate dimension.", "value");
            }

            m_element = (double[])value.GetElements().Clone();
        }

        public VTransform3D(
            double m00, double m01, double m02, double m03,
            double m10, double m11, double m12, double m13,
            double m20, double m21, double m22, double m23,
            double m30, double m31, double m32, double m33)
        {
            m_element = new double[4 * 4];

            m_element[0 * 4 + 0] = m00;
            m_element[1 * 4 + 0] = m01;
            m_element[2 * 4 + 0] = m02;
            m_element[3 * 4 + 0] = m03;

            m_element[0 * 4 + 1] = m10;
            m_element[1 * 4 + 1] = m11;
            m_element[2 * 4 + 1] = m12;
            m_element[3 * 4 + 1] = m13;

            m_element[0 * 4 + 2] = m20;
            m_element[1 * 4 + 2] = m21;
            m_element[2 * 4 + 2] = m22;
            m_element[3 * 4 + 2] = m23;

            m_element[0 * 4 + 3] = m30;
            m_element[1 * 4 + 3] = m31;
            m_element[2 * 4 + 3] = m32;
            m_element[3 * 4 + 3] = m33;
        }

        #endregion

        #region Public access methods.

        public double GetElement(int row, int col)
        {
            Debug.Assert(row >= 0 && row <= 3);
            Debug.Assert(col >= 0 && col <= 3);
            Init();
            return m_element[col * 4 + row];
        }

        public VTransform3D ChangeElement(int row, int col, double value)
        {
            Debug.Assert(row >= 0 && row <= 3);
            Debug.Assert(col >= 0 && col <= 3);
            Init();

            VTransform3D result = new VTransform3D();
            result.Init();
            for (int i = 0; i < 4 * 4; ++i) {
                result.m_element[i] = m_element[i];
            }
            result.m_element[col * 4 + row] = value;

            return result;
        }

        public static bool operator ==(VTransform3D valueL, VTransform3D valueR)
        {
            valueL.Init();
            valueR.Init();

            for (int i = 0; i < 4 * 4; ++i)
                if (valueL.m_element[i] != valueR.m_element[i])
                    return false;

            return true;
        }

        public static bool operator !=(VTransform3D valueL, VTransform3D valueR)
        {
            return !(valueL == valueR);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is VTransform3D))
                return false;

            return this == (VTransform3D)obj;
        }

        public bool Equals(VTransform3D transform)
        {
            return this == transform;
        }

        public override int GetHashCode()
        {
            Init();
            return base.GetHashCode();
        }

        public static VTransform3D Transpose(VTransform3D t)
        {
            t.Init();
            VTransform3D result = new VTransform3D();
            result.Init();
            result.m_element[0] = t.m_element[0];
            result.m_element[1] = t.m_element[4];
            result.m_element[2] = t.m_element[8];
            result.m_element[3] = t.m_element[12];
            result.m_element[4] = t.m_element[1];
            result.m_element[5] = t.m_element[5];
            result.m_element[6] = t.m_element[9];
            result.m_element[7] = t.m_element[13];
            result.m_element[8] = t.m_element[2];
            result.m_element[9] = t.m_element[6];
            result.m_element[10] = t.m_element[10];
            result.m_element[11] = t.m_element[14];
            result.m_element[12] = t.m_element[3];
            result.m_element[13] = t.m_element[7];
            result.m_element[14] = t.m_element[11];
            result.m_element[15] = t.m_element[15];
            return result;
        }

        /// <summary>
        /// Return the inverse of the matrix.
        /// </summary>
        /// <remarks>
        /// Uses Gauss-Jordan matrix inversion algorithm.
        /// See http://www.cs.berkeley.edu/~wkahan/MathH110/gji.pdf
        /// </remarks>
        /// <param name="t">Input matrix.</param>
        /// <returns>Inverse of t.</returns>
        public static VTransform3D Inverse(VTransform3D t)
        {
            t.Init();
            VTransform3D result = new VTransform3D(t);
            result.Init();

            double UFL = 5.9E-39;
            double G = 4.0;
            G = G / 3.0;
            G = G - 1.0;
            double EPS = Math.Abs(((G + G) - 1.0) + G);

            double[] maxColVal = new double[4];
            int[] colOrder = new int[4];

            for (int col = 0; col < 4; ++col) {
                maxColVal[col] = 0.0;
                for (int row = 0; row < 4; ++row) {
                    double x = Math.Abs(result.m_element[4 * row + col]);
                    if (x > maxColVal[col])
                        maxColVal[col] = x;
                }
            }

            for (int step = 0; step < 4; ++step) {
                //  Perform elimination upon column k.
                double pivotVal = 0.0;
                int pivotRow = step;
                for (int row = step; row < 4; ++row) {
                    double x = Math.Abs(result.m_element[4 * row + step]);
                    if (x > pivotVal) {
                        pivotVal = x;
                        pivotRow = row;
                    }
                }

                if (pivotVal == 0.0) {
                    pivotVal = EPS * maxColVal[step] + UFL;
                    result.m_element[4 * step + step] = pivotVal;
                }

                if (maxColVal[step] > 0.0) {
                    pivotVal = pivotVal / maxColVal[step];
                    if (pivotVal > G)
                        G = pivotVal;
                }

                colOrder[step] = pivotRow;
                if (pivotRow != step) {
                    for (int col = 0; col < 4; ++col) {
                        double temp = result.m_element[4 * pivotRow + col];
                        result.m_element[4 * pivotRow + col] = result.m_element[4 * step + col];
                        result.m_element[4 * step + col] = temp;
                    }
                }

                pivotVal = result.m_element[4 * step + step];
                result.m_element[4 * step + step] = 1.0;
                for (pivotRow = 0; pivotRow < 4; ++pivotRow)
                    result.m_element[4 * step + pivotRow] /= pivotVal;

                for (int row = 0; row < 4; ++row) {
                    if (row == step)
                        continue;

                    double temp = result.m_element[4 * row + step];
                    result.m_element[4 * row + step] = 0.0;
                    for (int col = 0; col < 4; ++col)
                        result.m_element[4 * row + col] -= result.m_element[4 * step + col] * temp;
                }
            }

            //  Unswap columns.
            for (int col = 3; col >= 0; --col) {
                int newCol = colOrder[col];
                if (newCol == col)
                    continue;

                for (int row = 0; row < 4; ++row) {
                    double temp = result.m_element[4 * row + col];
                    result.m_element[4 * row + col] = result.m_element[4 * row + newCol];
                    result.m_element[4 * row + newCol] = temp;
                }
            }

            //VTransform4D test = t.MultiplyBy(result);

            return result;
        }

        public static VTransform3D operator *(VTransform3D valueL, VTransform3D valueR)
        {
            valueL.Init();
            valueR.Init();

            VTransform3D result = new VTransform3D();
            result.Init();
            result.m_element[0] = valueL.m_element[0] * valueR.m_element[0] + valueL.m_element[4] * valueR.m_element[1] + valueL.m_element[8] * valueR.m_element[2] + valueL.m_element[12] * valueR.m_element[3];
            result.m_element[1] = valueL.m_element[1] * valueR.m_element[0] + valueL.m_element[5] * valueR.m_element[1] + valueL.m_element[9] * valueR.m_element[2] + valueL.m_element[13] * valueR.m_element[3];
            result.m_element[2] = valueL.m_element[2] * valueR.m_element[0] + valueL.m_element[6] * valueR.m_element[1] + valueL.m_element[10] * valueR.m_element[2] + valueL.m_element[14] * valueR.m_element[3];
            result.m_element[3] = valueL.m_element[3] * valueR.m_element[0] + valueL.m_element[7] * valueR.m_element[1] + valueL.m_element[11] * valueR.m_element[2] + valueL.m_element[15] * valueR.m_element[3];
            result.m_element[4] = valueL.m_element[0] * valueR.m_element[4] + valueL.m_element[4] * valueR.m_element[5] + valueL.m_element[8] * valueR.m_element[6] + valueL.m_element[12] * valueR.m_element[7];
            result.m_element[5] = valueL.m_element[1] * valueR.m_element[4] + valueL.m_element[5] * valueR.m_element[5] + valueL.m_element[9] * valueR.m_element[6] + valueL.m_element[13] * valueR.m_element[7];
            result.m_element[6] = valueL.m_element[2] * valueR.m_element[4] + valueL.m_element[6] * valueR.m_element[5] + valueL.m_element[10] * valueR.m_element[6] + valueL.m_element[14] * valueR.m_element[7];
            result.m_element[7] = valueL.m_element[3] * valueR.m_element[4] + valueL.m_element[7] * valueR.m_element[5] + valueL.m_element[11] * valueR.m_element[6] + valueL.m_element[15] * valueR.m_element[7];
            result.m_element[8] = valueL.m_element[0] * valueR.m_element[8] + valueL.m_element[4] * valueR.m_element[9] + valueL.m_element[8] * valueR.m_element[10] + valueL.m_element[12] * valueR.m_element[11];
            result.m_element[9] = valueL.m_element[1] * valueR.m_element[8] + valueL.m_element[5] * valueR.m_element[9] + valueL.m_element[9] * valueR.m_element[10] + valueL.m_element[13] * valueR.m_element[11];
            result.m_element[10] = valueL.m_element[2] * valueR.m_element[8] + valueL.m_element[6] * valueR.m_element[9] + valueL.m_element[10] * valueR.m_element[10] + valueL.m_element[14] * valueR.m_element[11];
            result.m_element[11] = valueL.m_element[3] * valueR.m_element[8] + valueL.m_element[7] * valueR.m_element[9] + valueL.m_element[11] * valueR.m_element[10] + valueL.m_element[15] * valueR.m_element[11];
            result.m_element[12] = valueL.m_element[0] * valueR.m_element[12] + valueL.m_element[4] * valueR.m_element[13] + valueL.m_element[8] * valueR.m_element[14] + valueL.m_element[12] * valueR.m_element[15];
            result.m_element[13] = valueL.m_element[1] * valueR.m_element[12] + valueL.m_element[5] * valueR.m_element[13] + valueL.m_element[9] * valueR.m_element[14] + valueL.m_element[13] * valueR.m_element[15];
            result.m_element[14] = valueL.m_element[2] * valueR.m_element[12] + valueL.m_element[6] * valueR.m_element[13] + valueL.m_element[10] * valueR.m_element[14] + valueL.m_element[14] * valueR.m_element[15];
            result.m_element[15] = valueL.m_element[3] * valueR.m_element[12] + valueL.m_element[7] * valueR.m_element[13] + valueL.m_element[11] * valueR.m_element[14] + valueL.m_element[15] * valueR.m_element[15];
            return result;
        }

        public VTransform3D MultiplyBy(VTransform3D rhs)
        {
            Init();
            rhs.Init();
            VTransform3D result = new VTransform3D();
            result.Init();
            result.m_element[0] = m_element[0] * rhs.m_element[0] + m_element[4] * rhs.m_element[1] + m_element[8] * rhs.m_element[2] + m_element[12] * rhs.m_element[3];
            result.m_element[1] = m_element[1] * rhs.m_element[0] + m_element[5] * rhs.m_element[1] + m_element[9] * rhs.m_element[2] + m_element[13] * rhs.m_element[3];
            result.m_element[2] = m_element[2] * rhs.m_element[0] + m_element[6] * rhs.m_element[1] + m_element[10] * rhs.m_element[2] + m_element[14] * rhs.m_element[3];
            result.m_element[3] = m_element[3] * rhs.m_element[0] + m_element[7] * rhs.m_element[1] + m_element[11] * rhs.m_element[2] + m_element[15] * rhs.m_element[3];
            result.m_element[4] = m_element[0] * rhs.m_element[4] + m_element[4] * rhs.m_element[5] + m_element[8] * rhs.m_element[6] + m_element[12] * rhs.m_element[7];
            result.m_element[5] = m_element[1] * rhs.m_element[4] + m_element[5] * rhs.m_element[5] + m_element[9] * rhs.m_element[6] + m_element[13] * rhs.m_element[7];
            result.m_element[6] = m_element[2] * rhs.m_element[4] + m_element[6] * rhs.m_element[5] + m_element[10] * rhs.m_element[6] + m_element[14] * rhs.m_element[7];
            result.m_element[7] = m_element[3] * rhs.m_element[4] + m_element[7] * rhs.m_element[5] + m_element[11] * rhs.m_element[6] + m_element[15] * rhs.m_element[7];
            result.m_element[8] = m_element[0] * rhs.m_element[8] + m_element[4] * rhs.m_element[9] + m_element[8] * rhs.m_element[10] + m_element[12] * rhs.m_element[11];
            result.m_element[9] = m_element[1] * rhs.m_element[8] + m_element[5] * rhs.m_element[9] + m_element[9] * rhs.m_element[10] + m_element[13] * rhs.m_element[11];
            result.m_element[10] = m_element[2] * rhs.m_element[8] + m_element[6] * rhs.m_element[9] + m_element[10] * rhs.m_element[10] + m_element[14] * rhs.m_element[11];
            result.m_element[11] = m_element[3] * rhs.m_element[8] + m_element[7] * rhs.m_element[9] + m_element[11] * rhs.m_element[10] + m_element[15] * rhs.m_element[11];
            result.m_element[12] = m_element[0] * rhs.m_element[12] + m_element[4] * rhs.m_element[13] + m_element[8] * rhs.m_element[14] + m_element[12] * rhs.m_element[15];
            result.m_element[13] = m_element[1] * rhs.m_element[12] + m_element[5] * rhs.m_element[13] + m_element[9] * rhs.m_element[14] + m_element[13] * rhs.m_element[15];
            result.m_element[14] = m_element[2] * rhs.m_element[12] + m_element[6] * rhs.m_element[13] + m_element[10] * rhs.m_element[14] + m_element[14] * rhs.m_element[15];
            result.m_element[15] = m_element[3] * rhs.m_element[12] + m_element[7] * rhs.m_element[13] + m_element[11] * rhs.m_element[14] + m_element[15] * rhs.m_element[15];
            return result;
        }

        public static VCoordinate3D operator *(VTransform3D transform, VCoordinate3D coord)
        {
            transform.Init();
            VCoordinate3D result = new VCoordinate3D();
            result.X = transform.m_element[0] * coord[0] + transform.m_element[4] * coord[1] + transform.m_element[8] * coord[2] + transform.m_element[12];
            result.Y = transform.m_element[1] * coord[0] + transform.m_element[5] * coord[1] + transform.m_element[9] * coord[2] + transform.m_element[13];
            result.Z = transform.m_element[2] * coord[0] + transform.m_element[6] * coord[1] + transform.m_element[10] * coord[2] + transform.m_element[14];
            double w = transform.m_element[3] * coord[0] + transform.m_element[7] * coord[1] + transform.m_element[11] * coord[2] + transform.m_element[15];
            //Debug.Assert(w != 0.0);
            w = 1.0 / w;
            result.X *= w;
            result.Y *= w;
            result.Z *= w;
            return result;
        }

        public static VCoordinate4D operator *(VTransform3D transform, VCoordinate4D coord)
        {
            transform.Init();
            VCoordinate4D result = new VCoordinate4D();
            result.X = transform.m_element[0] * coord[0] + transform.m_element[4] * coord[1] + transform.m_element[8] * coord[2] + transform.m_element[12] * coord[3];
            result.Y = transform.m_element[1] * coord[0] + transform.m_element[5] * coord[1] + transform.m_element[9] * coord[2] + transform.m_element[13] * coord[3];
            result.Z = transform.m_element[2] * coord[0] + transform.m_element[6] * coord[1] + transform.m_element[10] * coord[2] + transform.m_element[14] * coord[3];
            result.W = transform.m_element[3] * coord[0] + transform.m_element[7] * coord[1] + transform.m_element[11] * coord[2] + transform.m_element[15] * coord[3];
            return result;
        }

        public static Coordinate operator *(VTransform3D transform, Coordinate coord)
        {
            transform.Init();

            if (coord.Dimension < 3)
                coord = coord.ToDimension(3);
            else if (coord.Dimension > 4)
                coord = coord.ToDimension(4);

            if (coord.Dimension == 3) {
                Coordinate result = new Coordinate(3);
                result.X = transform.m_element[0] * coord.X + transform.m_element[4] * coord.Y + transform.m_element[8] * coord.Z + transform.m_element[12];
                result.Y = transform.m_element[1] * coord.X + transform.m_element[5] * coord.Y + transform.m_element[9] * coord.Z + transform.m_element[13];
                result.Z = transform.m_element[2] * coord.X + transform.m_element[6] * coord.Y + transform.m_element[10] * coord.Z + transform.m_element[14];
                double w = transform.m_element[3] * coord.X + transform.m_element[7] * coord.Y + transform.m_element[11] * coord.Z + transform.m_element[15];

                //Debug.Assert(w != 0.0);
                w = 1.0 / w;
                result.X *= w;
                result.Y *= w;
                result.Z *= w;
                return result;
            } else {
                Coordinate result = new Coordinate(4);
                result.X = transform.m_element[0] * coord.X + transform.m_element[4] * coord.Y + transform.m_element[8] * coord.Z + transform.m_element[12] * coord.W;
                result.Y = transform.m_element[1] * coord.X + transform.m_element[5] * coord.Y + transform.m_element[9] * coord.Z + transform.m_element[13] * coord.W;
                result.Z = transform.m_element[2] * coord.X + transform.m_element[6] * coord.Y + transform.m_element[10] * coord.Z + transform.m_element[14] * coord.W;
                result.W = transform.m_element[3] * coord.X + transform.m_element[7] * coord.Y + transform.m_element[11] * coord.Z + transform.m_element[15] * coord.W;
                return result;
            }
        }

        public static VTransform3D Identity()
        {
            VTransform3D result = new VTransform3D();
            result.Init();
            result.m_element[0] = 1.0;
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 0.0;
            result.m_element[5] = 1.0;
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 0.0;
            result.m_element[9] = 0.0;
            result.m_element[10] = 1.0;
            result.m_element[11] = 0.0;
            result.m_element[12] = 0.0;
            result.m_element[13] = 0.0;
            result.m_element[14] = 0.0;
            result.m_element[15] = 1.0;
            return result;
        }

        public static VTransform3D Translation(ICoordinate translation)
        {
            Debug.Assert(translation.Dimension == 3);
            VTransform3D result = new VTransform3D();
            result.Init();
            result.m_element[0] = 1.0;
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 0.0;
            result.m_element[5] = 1.0;
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 0.0;
            result.m_element[9] = 0.0;
            result.m_element[10] = 1.0;
            result.m_element[11] = 0.0;
            result.m_element[12] = translation[0];
            result.m_element[13] = translation[1];
            result.m_element[14] = translation[2];
            result.m_element[15] = 1.0;
            return result;
        }

        public static VTransform3D Scale(ICoordinate scale)
        {
            Debug.Assert(scale.Dimension == 3);
            VTransform3D result = new VTransform3D();
            result.Init();
            result.m_element[0] = scale[0];
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 0.0;
            result.m_element[5] = scale[1];
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 0.0;
            result.m_element[9] = 0.0;
            result.m_element[10] = scale[2];
            result.m_element[11] = 0.0;
            result.m_element[12] = 0.0;
            result.m_element[13] = 0.0;
            result.m_element[14] = 0.0;
            result.m_element[15] = 1.0;
            return result;
        }

        public static VTransform3D XRotation(double angle)
        {
            VTransform3D result = new VTransform3D();
            result.Init();
            double cosAngle = Math.Cos(angle);
            double sinAngle = Math.Sin(angle);
            result.m_element[0] = 1.0;
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 0.0;
            result.m_element[5] = cosAngle;
            result.m_element[6] = sinAngle;
            result.m_element[7] = 0.0;
            result.m_element[8] = 0.0;
            result.m_element[9] = -sinAngle;
            result.m_element[10] = cosAngle;
            result.m_element[11] = 0.0;
            result.m_element[12] = 0.0;
            result.m_element[13] = 0.0;
            result.m_element[14] = 0.0;
            result.m_element[15] = 1.0;
            return result;
        }

        public static VTransform3D YRotation(double angle)
        {
            VTransform3D result = new VTransform3D();
            result.Init();
            double cosAngle = Math.Cos(angle);
            double sinAngle = Math.Sin(angle);
            result.m_element[0] = cosAngle;
            result.m_element[1] = 0.0;
            result.m_element[2] = -sinAngle;
            result.m_element[3] = 0.0;
            result.m_element[4] = 0.0;
            result.m_element[5] = 1.0;
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = sinAngle;
            result.m_element[9] = 0.0;
            result.m_element[10] = cosAngle;
            result.m_element[11] = 0.0;
            result.m_element[12] = 0.0;
            result.m_element[13] = 0.0;
            result.m_element[14] = 0.0;
            result.m_element[15] = 1.0;
            return result;
        }

        public static VTransform3D ZRotation(double angle)
        {
            VTransform3D result = new VTransform3D();
            result.Init();
            double cosAngle = Math.Cos(angle);
            double sinAngle = Math.Sin(angle);
            result.m_element[0] = cosAngle;
            result.m_element[1] = sinAngle;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = -sinAngle;
            result.m_element[5] = cosAngle;
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 0.0;
            result.m_element[9] = 0.0;
            result.m_element[10] = 1.0;
            result.m_element[11] = 0.0;
            result.m_element[12] = 0.0;
            result.m_element[13] = 0.0;
            result.m_element[14] = 0.0;
            result.m_element[15] = 1.0;
            return result;
        }

        public static VTransform3D Projection(double projectionZ)
        {
            VTransform3D result = new VTransform3D();
            result.Init();
            result.m_element[0] = 1.0;
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 0.0;
            result.m_element[5] = 1.0;
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 0.0;
            result.m_element[9] = 0.0;
            result.m_element[10] = 1.0;
            result.m_element[11] = 1.0 / projectionZ;
            result.m_element[12] = 0.0;
            result.m_element[13] = 0.0;
            result.m_element[14] = 0.0;
            result.m_element[15] = 0.0;
            return result;
        }

        public static VTransform3D OrthoRH(double left, double right, double bottom, double top, double nearPlane, double farPlane)
        {
            VTransform3D result = new VTransform3D();
            result.Init();
            result.m_element[0] = 2.0 / (right - left);
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 0.0;
            result.m_element[5] = 2.0 / (top - bottom);
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 0.0;
            result.m_element[9] = 0.0;
            result.m_element[10] = 1.0 / (nearPlane - farPlane);
            result.m_element[11] = 0.0;
            result.m_element[12] = -(right + left) / (right - left);
            result.m_element[13] = -(top + bottom) / (top - bottom);
            result.m_element[14] = -(nearPlane) / (farPlane - nearPlane);
            result.m_element[15] = 1.0;
            return result;
        }

        public static VTransform3D OrthoLH(double left, double right, double bottom, double top, double nearPlane, double farPlane)
        {
            VTransform3D result = new VTransform3D();
            result.Init();
            result.m_element[0] = 2.0 / (right - left);
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 0.0;
            result.m_element[5] = 2.0 / (top - bottom);
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = 0.0;
            result.m_element[9] = 0.0;
            result.m_element[10] = 1.0 / (farPlane - nearPlane);
            result.m_element[11] = 0.0;
            result.m_element[12] = -(right + left) / (right - left);
            result.m_element[13] = -(top + bottom) / (top - bottom);
            result.m_element[14] = -(nearPlane) / (farPlane - nearPlane);
            result.m_element[15] = 1.0;
            return result;
        }

        public static VTransform3D FrustumRH(double left, double right, double bottom, double top, double nearPlane, double farPlane)
        {
            VTransform3D result = new VTransform3D();
            result.Init();
            result.m_element[0] = 2.0 * nearPlane / (right - left);
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 0.0;
            result.m_element[5] = 2.0 * nearPlane / (top - bottom);
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = (right + left) / (right - left);
            result.m_element[9] = (top + bottom) / (top - bottom);
            result.m_element[10] = -farPlane / (farPlane - nearPlane);
            result.m_element[11] = -1.0;
            result.m_element[12] = 0.0;
            result.m_element[13] = 0.0;
            result.m_element[14] = -farPlane * nearPlane / (farPlane - nearPlane);
            result.m_element[15] = 0.0;
            return result;
        }

        public static VTransform3D FrustumLH(double left, double right, double bottom, double top, double nearPlane, double farPlane)
        {
            VTransform3D result = new VTransform3D();
            result.Init();
            result.m_element[0] = 2.0 * nearPlane / (right - left);
            result.m_element[1] = 0.0;
            result.m_element[2] = 0.0;
            result.m_element[3] = 0.0;
            result.m_element[4] = 0.0;
            result.m_element[5] = 2.0 * nearPlane / (top - bottom);
            result.m_element[6] = 0.0;
            result.m_element[7] = 0.0;
            result.m_element[8] = -(right + left) / (right - left);
            result.m_element[9] = -(top + bottom) / (top - bottom);
            result.m_element[10] = farPlane / (farPlane - nearPlane);
            result.m_element[11] = 1.0;
            result.m_element[12] = 0.0;
            result.m_element[13] = 0.0;
            result.m_element[14] = -farPlane * nearPlane / (farPlane - nearPlane);
            result.m_element[15] = 0.0;
            return result;
        }

        #endregion

        #region Private methods.

        private void Init()
        {
            if (m_element == null)
                m_element = new double[4 * 4];
        }

        #endregion

        #region Private fields.

        private double[] m_element;

        #endregion

    }



    public struct VTransformND
    {

        #region Public properties.

        public int Dimension { get { return m_size - 1; } }
        public int Size { get { return m_size; } }

        #endregion

        #region Constructors.

        public VTransformND(int dimension)
        {
            m_size = dimension + 1;
            m_element = new double[m_size * m_size];
        }

        public VTransformND(int dimension, double[] elements)
        {
            Debug.Assert(dimension >= 1);
            Debug.Assert(elements != null);
            Debug.Assert(elements.Length == (dimension + 1) * (dimension + 1));

            m_size = dimension + 1;
            m_element = (double[])elements.Clone();
        }

        public VTransformND(VTransformND value)
        {
            m_size = value.m_size;
            m_element = new double[m_size * m_size];
            for (int i = 0; i < m_size * m_size; ++i)
                m_element[i] = value.m_element[i];
        }

        #endregion

        #region Public access methods.

        public double[] GetElements()
        {
            return (double[])m_element.Clone();
        }

        public double GetElement(int row, int col)
        {
            Debug.Assert(row >= 0 && row < m_size);
            Debug.Assert(col >= 0 && col < m_size);
            Init();
            return m_element[col * m_size + row];
        }

        public VTransformND ChangeElement(int row, int col, double value)
        {
            Debug.Assert(row >= 0 && row < m_size);
            Debug.Assert(col >= 0 && col < m_size);
            Init();

            VTransformND result = new VTransformND(this);
            result.Init();
            result.m_element[col * m_size + row] = value;

            return result;
        }

        public static bool operator ==(VTransformND valueL, VTransformND valueR)
        {
            Debug.Assert(valueL.Size == valueR.Size);
            int size = valueL.Size;

            valueL.Init();
            valueR.Init();

            for (int i = 0; i < size * size; ++i)
                if (valueL.m_element[i] != valueR.m_element[i])
                    return false;

            return true;
        }

        public static bool operator !=(VTransformND valueL, VTransformND valueR)
        {
            return !(valueL == valueR);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is VTransformND))
                return false;

            VTransformND transform = (VTransformND)obj;
            if (Size != transform.Size)
                return false;

            return this == transform;
        }

        public bool Equals(VTransformND transform)
        {
            return this == transform;
        }

        public override int GetHashCode()
        {
            Init();
            return base.GetHashCode();
        }

        public VTransformND ToDimension(int newDimension)
        {
            Debug.Assert(newDimension > 0);

            int oldDimension = Dimension;
            if (oldDimension == newDimension) {
                return this;
            }

            Init();

            VTransformND result = new VTransformND(newDimension);
            result.Init();

            int oldSize = Size;
            int newSize = result.Size;

            int smallerDimension = (oldDimension < newDimension) ? oldDimension : newDimension;
            for (int row = 0; row < smallerDimension; ++row) {
                for (int col = 0; col < smallerDimension; ++col) {
                    result.m_element[row * newSize + col] = m_element[row * oldSize + col];
                }
                result.m_element[row * newSize + newDimension] = m_element[row * oldSize + oldDimension];
            }
            for (int col = 0; col < smallerDimension; ++col) {
                result.m_element[newDimension * newSize + col] = m_element[oldDimension * oldSize + col];
            }
            result.m_element[newDimension * newSize + newDimension] = m_element[oldDimension * oldSize + oldDimension];

            return result;
        }

        public static bool IsSingular(VTransformND t)
        {
            bool returnValue = false;

            InverseInternal(t, out returnValue);

            return returnValue;
        }

        /// <summary>
        /// Return the inverse of the matrix.
        /// </summary>
        /// <remarks>
        /// Uses Gauss-Jordan matrix inversion algorithm.
        /// See http://www.cs.berkeley.edu/~wkahan/MathH110/gji.pdf
        /// </remarks>
        /// <param name="t">Input matrix.</param>
        /// <returns>Inverse of t.</returns>
        public static VTransformND Inverse(VTransformND t)
        {
            bool inputWasSingular;
            return InverseInternal(t, out inputWasSingular);
        }

        public static VTransformND Transpose(VTransformND t)
        {
            int dimension = t.Dimension;
            int size = dimension + 1;

            t.Init();
            VTransformND result = new VTransformND(dimension);
            result.Init();

            for (int row = 0; row < size; ++row) {
                for (int col = 0; col < size; ++col) {
                    result.m_element[row * size + col] = t.m_element[col * size + row];
                }
            }

            return result;
        }

        public VTransformND MultiplyBy(VTransformND rhs)
        {
            Debug.Assert(rhs.Size == Size);

            Init();
            rhs.Init();
            VTransformND result = new VTransformND(Dimension);
            result.Init();

            for (int row = 0; row < m_size; ++row) {
                for (int col = 0; col < m_size; ++col) {
                    double elementResult = 0.0;
                    for (int i = 0; i < m_size; ++i) {
                        elementResult += this.m_element[i * m_size + col] * rhs.m_element[row * m_size + i];
                    }

                    result.m_element[row * m_size + col] = elementResult;
                }
            }

            return result;
        }

        public static Coordinate operator *(VTransformND transform, Coordinate coord)
        {
            int size = transform.Size;
            int dimension = transform.Dimension;

            transform.Init();

            if (coord.Dimension < dimension)
                coord = coord.ToDimension(dimension);
            else if (coord.Dimension > size)
                coord = coord.ToDimension(size);

            if (coord.Dimension == dimension) {

                Coordinate result = new Coordinate(dimension);
                for (int row = 0; row < dimension; ++row) {
                    double rowResult = 0.0;
                    for (int col = 0; col < dimension; ++col) {
                        rowResult += transform.m_element[col * size + row] * coord[col];
                    }
                    rowResult += transform.m_element[dimension * size + row];
                    result[row] = rowResult;
                }

                double w = 0.0;
                for (int col = 0; col < dimension; ++col) {
                    w += transform.m_element[col * size + dimension] * coord[col];
                }
                w += transform.m_element[dimension * size + dimension];

                Debug.Assert(w != 0.0);
                w = 1.0 / w;
                result = result * w;

                return result;

            } else {

                Coordinate result = new Coordinate(size);
                for (int row = 0; row < size; ++row) {
                    double rowResult = 0.0;
                    for (int col = 0; col < size; ++col) {
                        rowResult += transform.m_element[col * size + row] * coord[col];
                    }
                    result[row] = rowResult;
                }

                return result;

            }
        }

        public bool IsIdentity()
        {
            bool result = true;
            int size = Size;
            Init();
            for (int row = 0; row < size; ++row) {
                for (int col = 0; col < size; ++col) {
                    if (row == col) {
                        if (m_element[row * size + col] != 1.0) {
                            result = false;
                            break;
                        }
                    } else {
                        if (m_element[row * size + col] != 0.0) {
                            result = false;
                            break;
                        }
                    }
                }
            }
            return result;
        }

        public static VTransformND Identity(int dimension)
        {
            VTransformND result = new VTransformND(dimension);
            int size = result.Size;
            result.Init();
            for (int row = 0; row < size; ++row) {
                for (int col = 0; col < size; ++col) {
                    if (row == col) {
                        result.m_element[row * size + col] = 1.0;
                    } else {
                        result.m_element[row * size + col] = 0.0;
                    }
                }
            }

            return result;
        }

        #endregion

        #region Private methods.

        private void Init()
        {
            Debug.Assert(m_size > 0);

            if (m_element == null)
                m_element = new double[m_size * m_size];
        }

        private static VTransformND InverseInternal(VTransformND t, out bool inputWasSingular)
        {
            int size = t.Size;

            t.Init();
            VTransformND result = new VTransformND(t);
            result.Init();

            inputWasSingular = false;
            double UFL = 5.9E-39;
            double G = 4.0;
            G = G / 3.0;
            G = G - 1.0;
            double EPS = Math.Abs(((G + G) - 1.0) + G);

            double[] maxColVal = new double[size];
            int[] colOrder = new int[size];

            for (int col = 0; col < size; ++col) {
                maxColVal[col] = 0.0;
                for (int row = 0; row < size; ++row) {
                    double x = Math.Abs(result.m_element[size * row + col]);
                    if (x > maxColVal[col])
                        maxColVal[col] = x;
                }
            }

            for (int step = 0; step < size; ++step) {
                //  Perform elimination upon column k.
                double pivotVal = 0.0;
                int pivotRow = step;
                for (int row = step; row < size; ++row) {
                    double x = Math.Abs(result.m_element[size * row + step]);
                    if (x > pivotVal) {
                        pivotVal = x;
                        pivotRow = row;
                    }
                }

                if (pivotVal == 0.0) {
                    inputWasSingular = true;
                    pivotVal = EPS * maxColVal[step] + UFL;
                    result.m_element[size * step + step] = pivotVal;
                }

                if (maxColVal[step] > 0.0) {
                    pivotVal = pivotVal / maxColVal[step];
                    if (pivotVal > G)
                        G = pivotVal;
                }

                colOrder[step] = pivotRow;
                if (pivotRow != step) {
                    for (int col = 0; col < size; ++col) {
                        double temp = result.m_element[size * pivotRow + col];
                        result.m_element[size * pivotRow + col] = result.m_element[size * step + col];
                        result.m_element[size * step + col] = temp;
                    }
                }

                pivotVal = result.m_element[size * step + step];
                result.m_element[size * step + step] = 1.0;
                for (pivotRow = 0; pivotRow < size; ++pivotRow)
                    result.m_element[size * step + pivotRow] /= pivotVal;

                for (int row = 0; row < size; ++row) {
                    if (row == step)
                        continue;

                    double temp = result.m_element[size * row + step];
                    result.m_element[size * row + step] = 0.0;
                    for (int col = 0; col < size; ++col)
                        result.m_element[size * row + col] -= result.m_element[size * step + col] * temp;
                }
            }

            //  Unswap columns.
            for (int col = size - 1; col >= 0; --col) {
                int newCol = colOrder[col];
                if (newCol == col)
                    continue;

                for (int row = 0; row < size; ++row) {
                    double temp = result.m_element[size * row + col];
                    result.m_element[size * row + col] = result.m_element[size * row + newCol];
                    result.m_element[size * row + newCol] = temp;
                }
            }

            //VTransformND test = t.MultiplyBy(result);

            return result;
        }

        #endregion

        #region Private fields.

        private int m_size;
        private double[] m_element;

        #endregion
    }

}
