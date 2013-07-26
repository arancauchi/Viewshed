using System;
using System.Diagnostics;

namespace Myriax.Eonfusion.API.Helpers
{

    public interface ICoordinate
    {

        #region Public properties.

        int Dimension { get; }

        double this[int d] { get; set; }

        double Length { get; }
        double LengthSquared { get; }

        #endregion

        #region CopyFrom and CloneTo.

        void CloneTo(ref ICoordinate result);
        void CopyFrom(ICoordinate value);

        #endregion

        #region Public access methods.

        void Set(ICoordinate value);

        Coordinate ToDimension(int dimension);

        #endregion

    }



    public class Coordinate : ICoordinate
    {

        #region Public properties.

        public int Dimension { get { return m_element.Length; } }

        public double this[int d]
        {
            get { return m_element[d]; }
            set { m_element[d] = value; }
        }

        public double Length
        {
            get
            {
                return Math.Sqrt(LengthSquared);
            }
        }

        public double LengthSquared
        {
            get
            {
                double result = 0d;
                for (int d = 0; d < Dimension; ++d)
                    result += this[d] * this[d];
                return result;
            }
        }

        public double X
        {
            get { return m_element[0]; }
            set { m_element[0] = value; }
        }

        public double Y
        {
            get { return m_element[1]; }
            set { m_element[1] = value; }
        }

        public double Z
        {
            get { return m_element[2]; }
            set { m_element[2] = value; }
        }

        public double T
        {
            get { return m_element[3]; }
            set { m_element[3] = value; }
        }

        public double W
        {
            get { return m_element[3]; }
            set { m_element[3] = value; }
        }

        #endregion

        #region CopyFrom and CloneTo.

        public void CloneTo(ref ICoordinate result)
        {
            result = new Coordinate(this);
        }

        public void CopyFrom(ICoordinate value)
        {
            Set(value);
        }

        public void CloneTo(ref Coordinate result)
        {
            result = new Coordinate(this);
        }

        public void CopyFrom(Coordinate value)
        {
            Set(value);
        }

        #endregion

        #region Public constructors and setters.

        public Coordinate()
        {
            m_element = new double[0];
        }

        public Coordinate(int dimension)
        {
            m_element = new double[dimension];
        }

        public Coordinate(Coordinate value)
        {
            m_element = new double[value.Dimension];
            Set(value);
        }

        public Coordinate(ICoordinate value)
        {
            m_element = new double[value.Dimension];
            Set(value);
        }

        public Coordinate(double x)
        {
            m_element = new double[1];
            m_element[0] = x;
        }

        public Coordinate(double x, double y)
        {
            m_element = new double[2];
            m_element[0] = x;
            m_element[1] = y;
        }

        public Coordinate(double x, double y, double z)
        {
            m_element = new double[3];
            m_element[0] = x;
            m_element[1] = y;
            m_element[2] = z;
        }

        public Coordinate(double x, double y, double z, double tw)
        {
            m_element = new double[4];
            m_element[0] = x;
            m_element[1] = y;
            m_element[2] = z;
            m_element[3] = tw;
        }

        public Coordinate(double element0, double element1, double element2, double element3, double element4)
        {
            m_element = new double[5];
            m_element[0] = element0;
            m_element[1] = element1;
            m_element[2] = element2;
            m_element[3] = element3;
            m_element[4] = element4;
        }

        public void Init(int dimension)
        {
            if (dimension != Dimension)
                m_element = new double[dimension];
        }

        public void Init(Coordinate value)
        {
            if (m_element.Length != value.m_element.Length)
                m_element = new double[value.m_element.Length];

            for (int i = 0; i < m_element.Length; ++i)
                m_element[i] = value.m_element[i];
        }

        public void Set(ICoordinate value)
        {
            Debug.Assert(value.Dimension == Dimension);

            for (int i = 0; i < m_element.Length; ++i)
                m_element[i] = value[i];
        }

        public void Set(double x)
        {
            Debug.Assert(Dimension == 1);
            m_element[0] = x;
        }

        public void Set(double x, double y)
        {
            Debug.Assert(Dimension == 2);
            m_element[0] = x;
            m_element[1] = y;
        }

        public void Set(double x, double y, double z)
        {
            Debug.Assert(Dimension == 3);
            m_element[0] = x;
            m_element[1] = y;
            m_element[2] = z;
        }

        public void Set(double x, double y, double z, double w)
        {
            Debug.Assert(Dimension == 4);
            m_element[0] = x;
            m_element[1] = y;
            m_element[2] = z;
            m_element[3] = w;
        }

        public void Set(double[] value)
        {
            Debug.Assert(value.Length == Dimension);

            for (int i = 0; i < m_element.Length; ++i)
                m_element[i] = value[i];
        }

        #endregion

        #region Public access methods.

        public static int Compare(Coordinate coordL, Coordinate coordR)
        {
            Debug.Assert(coordL.Dimension == coordR.Dimension);

            for (int d = 0; d < coordL.Dimension; ++d) {
                if (coordL[d] < coordR[d])
                    return -1;
                if (coordL[d] > coordR[d])
                    return +1;
            }

            return 0;
        }

        public static int Compare(Coordinate coordL, Coordinate coordR, int majorAxis)
        {
            Debug.Assert(coordL.Dimension == coordR.Dimension);
            Debug.Assert(majorAxis >= 0 && majorAxis < coordL.Dimension);

            if (coordL[majorAxis] < coordR[majorAxis])
                return -1;
            if (coordL[majorAxis] > coordR[majorAxis])
                return +1;

            for (int d = 0; d < majorAxis; ++d) {
                if (coordL[d] < coordR[d])
                    return -1;
                if (coordL[d] > coordR[d])
                    return +1;
            }

            for (int d = majorAxis + 1; d < coordL.Dimension; ++d) {
                if (coordL[d] < coordR[d])
                    return -1;
                if (coordL[d] > coordR[d])
                    return +1;
            }

            return 0;
        }

        public Coordinate Normal()
        {
            Coordinate result = new Coordinate(Dimension);

            double scale = 1d / Length;
            for (int d = 0; d < Dimension; ++d)
                result[d] = this[d] * scale;

            return result;
        }

        public double Dot(ICoordinate coord)
        {
            Debug.Assert(coord.Dimension == Dimension);

            double result = 0d;
            for (int d = 0; d < Dimension; ++d)
                result += this[d] * coord[d];

            return result;
        }

        public Coordinate ToDimension(int dimension)
        {
            Coordinate result = new Coordinate(dimension);

            if (dimension <= Dimension) {
                for (int d = 0; d < dimension; ++d)
                    result[d] = this[d];
            } else {
                for (int d = 0; d < Dimension; ++d)
                    result[d] = this[d];
            }

            return result;
        }

        public Coordinate Lower()
        {
            Debug.Assert(Dimension - 1 > 0);

            Coordinate result = new Coordinate(Dimension - 1);

            for (int d = 0; d < Dimension - 1; ++d)
                result[d] = this[d];

            return result;
        }

        public Coordinate Raise()
        {
            Coordinate result = new Coordinate(Dimension + 1);

            for (int d = 0; d < Dimension; ++d)
                result[d] = this[d];
            result[Dimension] = 1d;

            return result;
        }

        public Coordinate HomoNormalise()
        {
            Coordinate result = new Coordinate(Dimension);

            double scale = 1d / this[Dimension - 1];
            for (int d = 0; d < Dimension - 1; ++d)
                result[d] = this[d] * scale;
            result[Dimension - 1] = 1.0;

            return result;
        }

        public static Coordinate Perpendicular(Coordinate coord)
        {
            Debug.Assert(coord.Dimension >= 2);

            Coordinate result = new Coordinate(coord);
            result.X = -coord.Y;
            result.Y = coord.X;

            return result;
        }

        public static Coordinate Perpendicular(Coordinate coordL, Coordinate coordR)
        {
            Debug.Assert(coordL.Dimension >= 3);
            Debug.Assert(coordR.Dimension >= 3);

            Coordinate result = new Coordinate(coordL);
            result.X = coordL.Y * coordR.Z - coordL.Z * coordR.Y;
            result.Y = coordL.Z * coordR.X - coordL.X * coordR.Z;
            result.Z = coordL.X * coordR.Y - coordL.Y * coordR.X;

            return result;
        }

        public static bool operator ==(Coordinate coordL, Coordinate coordR)
        {
            if (ReferenceEquals(coordL, coordR))
                return true;
            if (ReferenceEquals(coordL, null) || ReferenceEquals(coordR, null))
                return false;

            Debug.Assert(coordL.Dimension == coordR.Dimension);

            for (int d = 0; d < coordL.Dimension; ++d)
                if (coordL[d] != coordR[d])
                    return false;

            return true;
        }

        public static bool operator !=(Coordinate coordL, Coordinate coordR)
        {
            if (ReferenceEquals(coordL, coordR))
                return false;
            if (ReferenceEquals(coordL, null) || ReferenceEquals(coordR, null))
                return true;

            Debug.Assert(coordL.Dimension == coordR.Dimension);

            for (int d = 0; d < coordL.Dimension; ++d)
                if (coordL[d] != coordR[d])
                    return true;

            return false;
        }

        public static bool operator <(Coordinate coordL, Coordinate coordR)
        {
            return Compare(coordL, coordR) < 0;
        }

        public static bool operator >(Coordinate coordL, Coordinate coordR)
        {
            return Compare(coordL, coordR) > 0;
        }

        public static bool operator <=(Coordinate coordL, Coordinate coordR)
        {
            return Compare(coordL, coordR) <= 0;
        }

        public static bool operator >=(Coordinate coordL, Coordinate coordR)
        {
            return Compare(coordL, coordR) >= 0;
        }

        public static Coordinate operator +(Coordinate coordL, Coordinate coordR)
        {
            Debug.Assert(coordL.Dimension == coordR.Dimension);

            Coordinate result = new Coordinate(coordL.Dimension);
            for (int d = 0; d < coordL.Dimension; ++d)
                result[d] = coordL[d] + coordR[d];

            return result;
        }

        public static Coordinate operator -(Coordinate coordL, Coordinate coordR)
        {
            Debug.Assert(coordL.Dimension == coordR.Dimension);

            Coordinate result = new Coordinate(coordL.Dimension);
            for (int d = 0; d < coordL.Dimension; ++d)
                result[d] = coordL[d] - coordR[d];

            return result;
        }

        public static Coordinate operator -(Coordinate coord)
        {
            Coordinate result = new Coordinate(coord.Dimension);
            for (int d = 0; d < coord.Dimension; ++d)
                result[d] = -coord[d];

            return result;
        }

        public static Coordinate operator *(Coordinate coordL, Coordinate coordR)
        {
            Debug.Assert(coordL.Dimension == coordR.Dimension);

            Coordinate result = new Coordinate(coordL.Dimension);
            for (int d = 0; d < coordL.Dimension; ++d)
                result[d] = coordL[d] * coordR[d];

            return result;
        }

        public static Coordinate operator *(Coordinate coord, double scale)
        {
            Coordinate result = new Coordinate(coord.Dimension);
            for (int d = 0; d < coord.Dimension; ++d)
                result[d] = coord[d] * scale;

            return result;
        }

        public static Coordinate operator /(Coordinate coordL, Coordinate coordR)
        {
            Debug.Assert(coordL.Dimension == coordR.Dimension);

            Coordinate result = new Coordinate(coordL.Dimension);
            for (int d = 0; d < coordL.Dimension; ++d)
                result[d] = coordL[d] / coordR[d];

            return result;
        }

        public static Coordinate operator /(Coordinate coord, double scale)
        {
            Coordinate result = new Coordinate(coord.Dimension);
            for (int d = 0; d < coord.Dimension; ++d)
                result[d] = coord[d] / scale;

            return result;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            Coordinate coord = obj as Coordinate;
            if (coord == null)
                return false;

            return this == coord;
        }

        public bool Equals(Coordinate coord)
        {
            return this == coord;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion

        #region Conversion to VCoordinateND vectors.

        public static explicit operator VCoordinate1D(Coordinate c)
        {
            Debug.Assert(c.Dimension == 1);
            return new VCoordinate1D(c.X);
        }

        public static explicit operator VCoordinate2D(Coordinate c)
        {
            Debug.Assert(c.Dimension == 2);
            return new VCoordinate2D(c.X, c.Y);
        }

        public static explicit operator VCoordinate3D(Coordinate c)
        {
            Debug.Assert(c.Dimension == 3);
            return new VCoordinate3D(c.X, c.Y, c.Z);
        }

        public static explicit operator VCoordinate4D(Coordinate c)
        {
            Debug.Assert(c.Dimension == 4);
            return new VCoordinate4D(c.X, c.Y, c.Z, c.W);
        }

        #endregion

        #region Private fields.

        private double[] m_element;

        #endregion

        #region Object overrides.

        public override string ToString()
        {
            string result = "(";
            for (int i = 0; i < Dimension; ++i) {
                result += this[i].ToString();
                if (i < Dimension - 1)
                    result += ", ";
            }
            result += ")";

            return result;
        }

        #endregion

    }



    public struct VCoordinate0D : ICoordinate
    {

        #region Public properties.

        public int Dimension { get { return 0; } }

        public bool HasSpecial { get { return false; } }


        public double this[int d]
        {
            get
            {
                Debug.Assert(d >= 0 && d < Dimension);
                return 0.0;
            }
            set
            {
                Debug.Assert(d >= 0 && d < Dimension);
            }
        }

        public double Length { get { return 0.0; } }

        public double LengthSquared { get { return 0.0; } }

        #endregion

        #region CopyFrom and CloneTo methods.

        public void CloneTo(ref VCoordinate0D result)
        {
            result = new VCoordinate0D(this);
        }

        public void CopyFrom(VCoordinate0D value)
        {
            Set(value);
        }

        public void CloneTo(ref ICoordinate result)
        {
            result = new VCoordinate0D(this);
        }

        public void CopyFrom(ICoordinate value)
        {
            Set(value);
        }

        #endregion

        #region Constructors and setters.

        public VCoordinate0D(ICoordinate value)
        {
            Set(value);
        }

        public void Set(ICoordinate value)
        {
            Debug.Assert(value.Dimension == Dimension);
        }

        public static implicit operator Coordinate(VCoordinate0D value)
        {
            return new Coordinate(value);
        }

        #endregion

        #region Public access methods.

        public int Compare(ICoordinate coord)
        {
            return Compare(this, (VCoordinate0D)coord);
        }

        public int Compare(ICoordinate coord, int majorAxis)
        {
            return Compare(this, (VCoordinate0D)coord);
        }

        public static int Compare(VCoordinate0D coordL, VCoordinate0D coordR)
        {
            return 0;
        }

        public static int Compare(VCoordinate0D coordL, VCoordinate0D coordR, int majorAxis)
        {
            Debug.Assert(majorAxis >= 0 && majorAxis < 0);

            return 0;
        }

        public VCoordinate0D Normal()
        {
            return new VCoordinate0D();
        }

        public double Dot(VCoordinate0D coord)
        {
            return 0.0;
        }

        public Coordinate ToDimension(int dimension)
        {
            return new Coordinate(dimension);
        }

        public VCoordinate1D Lift()
        {
            return new VCoordinate1D(1.0);
        }

        public VCoordinate0D HomoNormalise()
        {
            return new VCoordinate0D();
        }

        public static bool operator ==(VCoordinate0D coordL, VCoordinate0D coordR)
        {
            return true;
        }

        public static bool operator !=(VCoordinate0D coordL, VCoordinate0D coordR)
        {
            return false;
        }

        public static VCoordinate0D operator +(VCoordinate0D coordL, VCoordinate0D coordR)
        {
            return new VCoordinate0D();
        }

        public static VCoordinate0D operator -(VCoordinate0D coordL, VCoordinate0D coordR)
        {
            return new VCoordinate0D();
        }

        public static VCoordinate0D operator -(VCoordinate0D coord)
        {
            return new VCoordinate0D();
        }

        public static VCoordinate0D operator *(VCoordinate0D coordL, VCoordinate0D coordR)
        {
            return new VCoordinate0D();
        }

        public static VCoordinate0D operator *(VCoordinate0D coord, double scale)
        {
            return new VCoordinate0D();
        }

        public static VCoordinate0D operator /(VCoordinate0D coordL, VCoordinate0D coordR)
        {
            return new VCoordinate0D();
        }

        public static VCoordinate0D operator /(VCoordinate0D coord, double scale)
        {
            return new VCoordinate0D();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is VCoordinate0D))
                return false;

            VCoordinate0D coord = (VCoordinate0D)obj;
            return this == coord;
        }

        public bool Equals(VCoordinate0D coord)
        {
            return this == coord;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion

        #region Private fields.

        #endregion

        #region Object level overrides.

        public override string ToString()
        {
            return string.Format("()");
        }

        #endregion

    }



    public struct VCoordinate1D : ICoordinate
    {

        #region Public properties.

        public int Dimension { get { return 1; } }

        public double this[int d]
        {
            get
            {
                Debug.Assert(d >= 0 && d < Dimension);
                return m_element0;
            }
            set
            {
                Debug.Assert(d >= 0 && d < Dimension);
                m_element0 = value;
            }
        }

        public double Length
        {
            get { return Math.Abs(m_element0); }
        }

        public double LengthSquared
        {
            get { return m_element0 * m_element0; }
        }

        public double X
        {
            get { return m_element0; }
            set { m_element0 = value; }
        }

        #endregion

        #region CopyFrom and CloneTo methods.

        public void CloneTo(ref VCoordinate1D result)
        {
            result = new VCoordinate1D(this);
        }

        public void CopyFrom(VCoordinate1D value)
        {
            Set(value);
        }

        public void CloneTo(ref ICoordinate result)
        {
            result = new VCoordinate1D(this);
        }

        public void CopyFrom(ICoordinate value)
        {
            Set(value);
        }

        #endregion

        #region Constructors and setters.

        public VCoordinate1D(double x)
        {
            m_element0 = x;
        }

        public VCoordinate1D(ICoordinate value)
        {
            m_element0 = value[0];
        }

        public void Set(ICoordinate value)
        {
            Debug.Assert(value.Dimension == Dimension);
            m_element0 = value[0];
        }

        public void Set(double x)
        {
            m_element0 = x;
        }

        public static implicit operator Coordinate(VCoordinate1D value)
        {
            return new Coordinate(value.m_element0);
        }

        #endregion

        #region Public access methods.

        public int Compare(ICoordinate coord)
        {
            return Compare(this, (VCoordinate1D)coord);
        }

        public int Compare(ICoordinate coord, int majorAxis)
        {
            return Compare(this, (VCoordinate1D)coord);
        }

        public static int Compare(VCoordinate1D coordL, VCoordinate1D coordR)
        {
            if (coordL.m_element0 < coordR.m_element0)
                return -1;
            if (coordL.m_element0 > coordR.m_element0)
                return +1;

            return 0;
        }

        public static int Compare(VCoordinate1D coordL, VCoordinate1D coordR, int majorAxis)
        {
            Debug.Assert(majorAxis >= 0 && majorAxis < 1);

            if (coordL.m_element0 < coordR.m_element0)
                return -1;
            if (coordL.m_element0 > coordR.m_element0)
                return +1;

            return 0;
        }

        public VCoordinate1D Normal()
        {
            return new VCoordinate1D(1.0);
        }

        public double Dot(VCoordinate1D coord)
        {
            return m_element0 * coord.m_element0;
        }

        public Coordinate ToDimension(int dimension)
        {
            Coordinate result = new Coordinate(dimension);

            if (dimension > 0)
                result[0] = m_element0;

            return result;
        }

        public VCoordinate2D Lift()
        {
            return new VCoordinate2D(m_element0, 1.0);
        }

        public VCoordinate1D HomoNormalise()
        {
            return new VCoordinate1D(1.0);
        }

        public static bool operator ==(VCoordinate1D coordL, VCoordinate1D coordR)
        {
            if (coordL.m_element0 != coordR.m_element0)
                return false;

            return true;
        }

        public static bool operator !=(VCoordinate1D coordL, VCoordinate1D coordR)
        {
            if (coordL.m_element0 != coordR.m_element0)
                return true;

            return false;
        }

        public static VCoordinate1D operator +(VCoordinate1D coordL, VCoordinate1D coordR)
        {
            return new VCoordinate1D(coordL.m_element0 + coordR.m_element0);
        }

        public static VCoordinate1D operator -(VCoordinate1D coordL, VCoordinate1D coordR)
        {
            return new VCoordinate1D(coordL.m_element0 - coordR.m_element0);
        }

        public static VCoordinate1D operator -(VCoordinate1D coord)
        {
            return new VCoordinate1D(-coord.m_element0);
        }

        public static VCoordinate1D operator *(VCoordinate1D coordL, VCoordinate1D coordR)
        {
            return new VCoordinate1D(coordL.m_element0 * coordR.m_element0);
        }

        public static VCoordinate1D operator *(VCoordinate1D coord, double scale)
        {
            return new VCoordinate1D(coord.m_element0 * scale);
        }

        public static VCoordinate1D operator /(VCoordinate1D coordL, VCoordinate1D coordR)
        {
            return new VCoordinate1D(coordL.m_element0 / coordR.m_element0);
        }

        public static VCoordinate1D operator /(VCoordinate1D coordL, double scale)
        {
            return new VCoordinate1D(coordL.m_element0 / scale);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is VCoordinate1D))
                return false;

            VCoordinate1D coord = (VCoordinate1D)obj;
            return this == coord;
        }

        public bool Equals(VCoordinate1D coord)
        {
            return this == coord;
        }

        public override int GetHashCode()
        {
            return m_element0.GetHashCode();
        }

        #endregion

        #region Private fields.

        private double m_element0;

        #endregion

        #region Object level overrides.

        public override string ToString()
        {
            return string.Format("({0})", X);
        }

        #endregion

    }



    public struct VCoordinate2D : ICoordinate
    {

        #region Public properties.

        public int Dimension { get { return 2; } }

        public double this[int d]
        {
            get
            {
                Debug.Assert(d >= 0 && d < Dimension);
                switch (d) {
                    case 0: return m_element0;
                    case 1: return m_element1;
                }
                return 0.0;
            }
            set
            {
                Debug.Assert(d >= 0 && d < Dimension);
                switch (d) {
                    case 0: m_element0 = value; break;
                    case 1: m_element1 = value; break;
                }
            }
        }

        public double Length
        {
            get { return Math.Sqrt(LengthSquared); }
        }

        public double LengthSquared
        {
            get { return m_element0 * m_element0 + m_element1 * m_element1; }
        }

        public double X
        {
            get { return m_element0; }
            set { m_element0 = value; }
        }

        public double Y
        {
            get { return m_element1; }
            set { m_element1 = value; }
        }

        #endregion

        #region CopyFrom and CloneTo methods.

        public void CloneTo(ref VCoordinate2D result)
        {
            result = new VCoordinate2D(this);
        }

        public void CopyFrom(VCoordinate2D value)
        {
            Set(value);
        }

        public void CloneTo(ref ICoordinate result)
        {
            result = new VCoordinate2D(this);
        }

        public void CopyFrom(ICoordinate value)
        {
            Set(value);
        }

        #endregion

        #region Constructors and setters.

        public VCoordinate2D(double x, double y)
        {
            m_element0 = x;
            m_element1 = y;
        }

        public VCoordinate2D(ICoordinate value)
        {
            m_element0 = value[0];
            m_element1 = value[1];
        }

        public void Set(ICoordinate value)
        {
            m_element0 = value[0];
            m_element1 = value[1];
        }

        public void Set(double x, double y)
        {
            m_element0 = x;
            m_element1 = y;
        }

        public static implicit operator Coordinate(VCoordinate2D value)
        {
            return new Coordinate(value.m_element0, value.m_element1);
        }

        #endregion

        #region Public access methods.

        public int Compare(ICoordinate coord)
        {
            return Compare(this, (VCoordinate2D)coord);
        }

        public int Compare(ICoordinate coord, int majorAxis)
        {
            return Compare(this, (VCoordinate2D)coord);
        }

        public static int Compare(VCoordinate2D coordL, VCoordinate2D coordR)
        {
            if (coordL.m_element0 < coordR.m_element0)
                return -1;
            if (coordL.m_element0 > coordR.m_element0)
                return +1;
            if (coordL.m_element1 < coordR.m_element1)
                return -1;
            if (coordL.m_element1 > coordR.m_element1)
                return +1;

            return 0;
        }

        public static int Compare(VCoordinate2D coordL, VCoordinate2D coordR, int majorAxis)
        {
            Debug.Assert(majorAxis >= 0 && majorAxis < 2);

            if (coordL[majorAxis] < coordR[majorAxis])
                return -1;
            if (coordL[majorAxis] > coordR[majorAxis])
                return +1;

            if (coordL.m_element0 < coordR.m_element0)
                return -1;
            if (coordL.m_element0 > coordR.m_element0)
                return +1;
            if (coordL.m_element1 < coordR.m_element1)
                return -1;
            if (coordL.m_element1 > coordR.m_element1)
                return +1;

            return 0;
        }

        public VCoordinate2D Normal()
        {
            double scale = 1.0 / Length;
            return new VCoordinate2D(m_element0 * scale, m_element1 * scale);
        }

        public double Dot(VCoordinate2D coord)
        {
            return m_element0 * coord.m_element0 + m_element1 * coord.m_element1;
        }

        public Coordinate ToDimension(int dimension)
        {
            Coordinate result = new Coordinate(dimension);

            if (dimension > 0)
                result[0] = m_element0;
            if (dimension > 1)
                result[1] = m_element1;

            return result;
        }

        public VCoordinate3D Lift()
        {
            return new VCoordinate3D(m_element0, m_element1, 1.0);
        }

        public VCoordinate1D Lower()
        {
            return new VCoordinate1D(m_element0);
        }

        public VCoordinate2D HomoNormalise()
        {
            double scale = 1.0 / m_element1;
            return new VCoordinate2D(m_element0 * scale, 1.0);
        }

        public static bool operator ==(VCoordinate2D coordL, VCoordinate2D coordR)
        {
            if (coordL.m_element0 != coordR.m_element0)
                return false;
            if (coordL.m_element1 != coordR.m_element1)
                return false;

            return true;
        }

        public static bool operator !=(VCoordinate2D coordL, VCoordinate2D coordR)
        {
            if (coordL.m_element0 != coordR.m_element0)
                return true;
            if (coordL.m_element1 != coordR.m_element1)
                return true;

            return false;
        }

        public static VCoordinate2D operator +(VCoordinate2D coordL, VCoordinate2D coordR)
        {
            return new VCoordinate2D(coordL.m_element0 + coordR.m_element0, coordL.m_element1 + coordR.m_element1);
        }

        public static VCoordinate2D operator -(VCoordinate2D coordL, VCoordinate2D coordR)
        {
            return new VCoordinate2D(coordL.m_element0 - coordR.m_element0, coordL.m_element1 - coordR.m_element1);
        }

        public static VCoordinate2D operator -(VCoordinate2D coord)
        {
            return new VCoordinate2D(-coord.m_element0, -coord.m_element1);
        }

        public static VCoordinate2D operator *(VCoordinate2D coordL, VCoordinate2D coordR)
        {
            return new VCoordinate2D(coordL.m_element0 * coordR.m_element0, coordL.m_element1 * coordR.m_element1);
        }

        public static VCoordinate2D operator *(VCoordinate2D coord, double scale)
        {
            return new VCoordinate2D(coord.m_element0 * scale, coord.m_element1 * scale);
        }

        public static VCoordinate2D operator /(VCoordinate2D coordL, VCoordinate2D coordR)
        {
            return new VCoordinate2D(coordL.m_element0 / coordR.m_element0, coordL.m_element1 / coordR.m_element1);
        }

        public static VCoordinate2D operator /(VCoordinate2D coord, double scale)
        {
            return new VCoordinate2D(coord.m_element0 / scale, coord.m_element1 / scale);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is VCoordinate2D))
                return false;

            VCoordinate2D coord = (VCoordinate2D)obj;
            return this == coord;
        }

        public bool Equals(VCoordinate2D coord)
        {
            return this == coord;
        }

        public override int GetHashCode()
        {
            return m_element0.GetHashCode() ^ m_element1.GetHashCode();
        }

        #endregion

        #region Private fields.

        private double m_element0;
        private double m_element1;

        #endregion

        #region Object level overrides.

        public override string ToString()
        {
            return string.Format("({0}, {1})", X, Y);
        }

        #endregion

    }



    public struct VCoordinate3D : ICoordinate
    {

        #region Public properties.

        public int Dimension { get { return 3; } }

        public double this[int d]
        {
            get
            {
                Debug.Assert(d >= 0 && d < Dimension);
                switch (d) {
                    case 0: return m_element0;
                    case 1: return m_element1;
                    case 2: return m_element2;
                }
                return 0.0;
            }
            set
            {
                Debug.Assert(d >= 0 && d < Dimension);
                switch (d) {
                    case 0: m_element0 = value; break;
                    case 1: m_element1 = value; break;
                    case 2: m_element2 = value; break;
                }
            }
        }

        public double Length
        {
            get { return Math.Sqrt(LengthSquared); }
        }

        public double LengthSquared
        {
            get { return m_element0 * m_element0 + m_element1 * m_element1 + m_element2 * m_element2; }
        }

        public double X
        {
            get { return m_element0; }
            set { m_element0 = value; }
        }

        public double Y
        {
            get { return m_element1; }
            set { m_element1 = value; }
        }

        public double Z
        {
            get { return m_element2; }
            set { m_element2 = value; }
        }

        #endregion

        #region CopyFrom and CloneTo methods.

        public void CloneTo(ref VCoordinate3D result)
        {
            result = new VCoordinate3D(this);
        }

        public void CopyFrom(VCoordinate3D value)
        {
            Set(value);
        }

        public void CloneTo(ref ICoordinate result)
        {
            result = new VCoordinate3D(this);
        }

        public void CopyFrom(ICoordinate value)
        {
            Set(value);
        }

        #endregion

        #region Constructors and setters.

        public VCoordinate3D(double x, double y, double z)
        {
            m_element0 = x;
            m_element1 = y;
            m_element2 = z;
        }

        public VCoordinate3D(ICoordinate value)
        {
            m_element0 = value[0];
            m_element1 = value[1];
            m_element2 = value[2];
        }

        public void Set(ICoordinate value)
        {
            Debug.Assert(value.Dimension == Dimension);
            m_element0 = value[0];
            m_element1 = value[1];
            m_element2 = value[2];
        }

        public void Set(double x, double y, double z)
        {
            m_element0 = x;
            m_element1 = y;
            m_element2 = z;
        }

        public static implicit operator Coordinate(VCoordinate3D value)
        {
            return new Coordinate(value.m_element0, value.m_element1, value.m_element2);
        }

        #endregion

        #region Public access methods.

        public int Compare(ICoordinate coord)
        {
            return Compare(this, (VCoordinate3D)coord);
        }

        public int Compare(ICoordinate coord, int majorAxis)
        {
            return Compare(this, (VCoordinate3D)coord);
        }

        public static int Compare(VCoordinate3D coordL, VCoordinate3D coordR)
        {
            if (coordL.m_element0 < coordR.m_element0)
                return -1;
            if (coordL.m_element0 > coordR.m_element0)
                return +1;
            if (coordL.m_element1 < coordR.m_element1)
                return -1;
            if (coordL.m_element1 > coordR.m_element1)
                return +1;
            if (coordL.m_element2 < coordR.m_element2)
                return -1;
            if (coordL.m_element2 > coordR.m_element2)
                return +1;

            return 0;
        }

        public static int Compare(VCoordinate3D coordL, VCoordinate3D coordR, int majorAxis)
        {
            Debug.Assert(majorAxis >= 0 && majorAxis < 3);

            if (coordL[majorAxis] < coordR[majorAxis])
                return -1;
            if (coordL[majorAxis] > coordR[majorAxis])
                return +1;

            if (coordL.m_element0 < coordR.m_element0)
                return -1;
            if (coordL.m_element0 > coordR.m_element0)
                return +1;
            if (coordL.m_element1 < coordR.m_element1)
                return -1;
            if (coordL.m_element1 > coordR.m_element1)
                return +1;
            if (coordL.m_element2 < coordR.m_element2)
                return -1;
            if (coordL.m_element2 > coordR.m_element2)
                return +1;

            return 0;
        }

        public VCoordinate3D Normal()
        {
            double scale = 1.0 / Length;
            return new VCoordinate3D(m_element0 * scale, m_element1 * scale, m_element2 * scale);
        }

        public double Dot(VCoordinate3D coord)
        {
            return m_element0 * coord.m_element0 + m_element1 * coord.m_element1 + m_element2 * coord.m_element2;
        }

        public Coordinate ToDimension(int dimension)
        {
            Coordinate result = new Coordinate(dimension);

            if (dimension > 0)
                result[0] = m_element0;
            if (dimension > 1)
                result[1] = m_element1;
            if (dimension > 2)
                result[2] = m_element2;

            return result;
        }

        public VCoordinate4D Lift()
        {
            return new VCoordinate4D(m_element0, m_element1, m_element2, 1.0);
        }

        public VCoordinate2D Lower()
        {
            return new VCoordinate2D(m_element0, m_element1);
        }

        public VCoordinate3D HomoNormalise()
        {
            double scale = 1.0 / m_element2;
            return new VCoordinate3D(m_element0 * scale, m_element1 * scale, 1.0);
        }

        public static VCoordinate3D Perpendicular(VCoordinate3D coordL, VCoordinate3D coordR)
        {
            return new VCoordinate3D(
                coordL.Y * coordR.Z - coordL.Z * coordR.Y,
                coordL.Z * coordR.X - coordL.X * coordR.Z,
                coordL.X * coordR.Y - coordL.Y * coordR.X);
        }

        public static bool operator ==(VCoordinate3D coordL, VCoordinate3D coordR)
        {
            if (coordL.m_element0 != coordR.m_element0)
                return false;
            if (coordL.m_element1 != coordR.m_element1)
                return false;
            if (coordL.m_element2 != coordR.m_element2)
                return false;

            return true;
        }

        public static bool operator !=(VCoordinate3D coordL, VCoordinate3D coordR)
        {
            if (coordL.m_element0 != coordR.m_element0)
                return true;
            if (coordL.m_element1 != coordR.m_element1)
                return true;
            if (coordL.m_element2 != coordR.m_element2)
                return true;

            return false;
        }

        public static VCoordinate3D operator +(VCoordinate3D coordL, VCoordinate3D coordR)
        {
            return new VCoordinate3D(coordL.m_element0 + coordR.m_element0, coordL.m_element1 + coordR.m_element1, coordL.m_element2 + coordR.m_element2);
        }

        public static VCoordinate3D operator -(VCoordinate3D coordL, VCoordinate3D coordR)
        {
            return new VCoordinate3D(coordL.m_element0 - coordR.m_element0, coordL.m_element1 - coordR.m_element1, coordL.m_element2 - coordR.m_element2);
        }

        public static VCoordinate3D operator -(VCoordinate3D coord)
        {
            return new VCoordinate3D(-coord.m_element0, -coord.m_element1, -coord.m_element2);
        }

        public static VCoordinate3D operator *(VCoordinate3D coordL, VCoordinate3D coordR)
        {
            return new VCoordinate3D(coordL.m_element0 * coordR.m_element0, coordL.m_element1 * coordR.m_element1, coordL.m_element2 * coordR.m_element2);
        }

        public static VCoordinate3D operator *(VCoordinate3D coord, double scale)
        {
            return new VCoordinate3D(coord.m_element0 * scale, coord.m_element1 * scale, coord.m_element2 * scale);
        }

        public static VCoordinate3D operator /(VCoordinate3D coordL, VCoordinate3D coordR)
        {
            return new VCoordinate3D(coordL.m_element0 / coordR.m_element0, coordL.m_element1 / coordR.m_element1, coordL.m_element2 / coordR.m_element2);
        }

        public static VCoordinate3D operator /(VCoordinate3D coordL, double scale)
        {
            return new VCoordinate3D(coordL.m_element0 / scale, coordL.m_element1 / scale, coordL.m_element2 / scale);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is VCoordinate3D))
                return false;

            VCoordinate3D coord = (VCoordinate3D)obj;
            return this == coord;
        }

        public bool Equals(VCoordinate3D coord)
        {
            return this == coord;
        }

        public override int GetHashCode()
        {
            return m_element0.GetHashCode() ^ m_element1.GetHashCode() ^ m_element2.GetHashCode();
        }

        #endregion

        #region Private fields.

        private double m_element0;
        private double m_element1;
        private double m_element2;

        #endregion

        #region Object level overrides.

        public override string ToString()
        {
            return string.Format("({0}, {1}, {2})", X, Y, Z);
        }

        #endregion

    }



    public struct VCoordinate4D : ICoordinate
    {

        #region Public properties.

        public int Dimension { get { return 4; } }

        public double this[int d]
        {
            get
            {
                Debug.Assert(d >= 0 && d < Dimension);
                switch (d) {
                    case 0: return m_element0;
                    case 1: return m_element1;
                    case 2: return m_element2;
                    case 3: return m_element3;
                }
                return 0.0;
            }
            set
            {
                Debug.Assert(d >= 0 && d < Dimension);
                switch (d) {
                    case 0: m_element0 = value; break;
                    case 1: m_element1 = value; break;
                    case 2: m_element2 = value; break;
                    case 3: m_element3 = value; break;
                }
            }
        }

        public double Length
        {
            get { return Math.Sqrt(LengthSquared); }
        }

        public double LengthSquared
        {
            get { return m_element0 * m_element0 + m_element1 * m_element1 + m_element2 * m_element2 + m_element3 * m_element3; }
        }

        public double X
        {
            get { return m_element0; }
            set { m_element0 = value; }
        }

        public double Y
        {
            get { return m_element1; }
            set { m_element1 = value; }
        }

        public double Z
        {
            get { return m_element2; }
            set { m_element2 = value; }
        }

        public double T
        {
            get { return m_element3; }
            set { m_element3 = value; }
        }

        public double W
        {
            get { return m_element3; }
            set { m_element3 = value; }
        }

        #endregion

        #region CopyFrom and CloneTo methods.

        public void CloneTo(ref VCoordinate4D result)
        {
            result = new VCoordinate4D(this);
        }

        public void CopyFrom(VCoordinate4D value)
        {
            Set(value);
        }

        public void CloneTo(ref ICoordinate result)
        {
            result = new VCoordinate4D(this);
        }

        public void CopyFrom(ICoordinate value)
        {
            Set(value);
        }

        #endregion

        #region Constructors and setters.

        public VCoordinate4D(double x, double y, double z, double tw)
        {
            m_element0 = x;
            m_element1 = y;
            m_element2 = z;
            m_element3 = tw;
        }

        public VCoordinate4D(ICoordinate value, double y, double z, double w)
        {
            Debug.Assert(value.Dimension == 1);
            m_element0 = value[0];
            m_element1 = y;
            m_element2 = z;
            m_element3 = w;
        }

        public VCoordinate4D(ICoordinate value, double z, double w)
        {
            Debug.Assert(value.Dimension == 2);
            m_element0 = value[0];
            m_element1 = value[1];
            m_element2 = z;
            m_element3 = w;
        }


        public VCoordinate4D(ICoordinate value, double w)
        {
            Debug.Assert(value.Dimension == 3);
            m_element0 = value[0];
            m_element1 = value[1];
            m_element2 = value[2];
            m_element3 = w;
        }

        public VCoordinate4D(ICoordinate value)
        {
            Debug.Assert(value.Dimension == 4);

            m_element0 = value[0];
            m_element1 = value[1];
            m_element2 = value[2];
            m_element3 = value[3];
        }


        public void Set(ICoordinate value)
        {
            Debug.Assert(value.Dimension == 4);

            m_element0 = value[0];
            m_element1 = value[1];
            m_element2 = value[2];
            m_element3 = value[3];
        }

        public void Set(double x, double y, double z, double tw)
        {
            m_element0 = x;
            m_element1 = y;
            m_element2 = z;
            m_element3 = tw;
        }

        public static implicit operator Coordinate(VCoordinate4D value)
        {
            return new Coordinate(value.m_element0, value.m_element1, value.m_element2, value.m_element3);
        }

        ///// <summary>
        ///// Special deserialization constructor.
        ///// Required as part of implementing ISerializable.
        ///// </summary>
        ///// <param name="info"></param>
        ///// <param name="context"></param>
        //public VCoordinate4D(SerializationInfo info, StreamingContext context)
        //{
        //    fixed (double* element = m_element) {
        //        element[0] = info.GetDouble("X");
        //        element[1] = info.GetDouble("Y");
        //        element[2] = info.GetDouble("Z");
        //        element[3] = info.GetDouble("T");
        //    }
        //}

        #endregion

        #region Public access methods.

        public int Compare(ICoordinate coord)
        {
            return Compare(this, (VCoordinate4D)coord);
        }

        public int Compare(ICoordinate coord, int majorAxis)
        {
            return Compare(this, (VCoordinate4D)coord);
        }

        public static int Compare(VCoordinate4D coordL, VCoordinate4D coordR)
        {
            if (coordL.m_element0 < coordR.m_element0)
                return -1;
            if (coordL.m_element0 > coordR.m_element0)
                return +1;
            if (coordL.m_element1 < coordR.m_element1)
                return -1;
            if (coordL.m_element1 > coordR.m_element1)
                return +1;
            if (coordL.m_element2 < coordR.m_element2)
                return -1;
            if (coordL.m_element2 > coordR.m_element2)
                return +1;
            if (coordL.m_element3 < coordR.m_element3)
                return -1;
            if (coordL.m_element3 > coordR.m_element3)
                return +1;

            return 0;
        }

        public static int Compare(VCoordinate4D coordL, VCoordinate4D coordR, int majorAxis)
        {
            Debug.Assert(majorAxis >= 0 && majorAxis < 4);

            if (coordL[majorAxis] < coordR[majorAxis])
                return -1;
            if (coordL[majorAxis] > coordR[majorAxis])
                return +1;

            if (coordL.m_element0 < coordR.m_element0)
                return -1;
            if (coordL.m_element0 > coordR.m_element0)
                return +1;
            if (coordL.m_element1 < coordR.m_element1)
                return -1;
            if (coordL.m_element1 > coordR.m_element1)
                return +1;
            if (coordL.m_element2 < coordR.m_element2)
                return -1;
            if (coordL.m_element2 > coordR.m_element2)
                return +1;
            if (coordL.m_element3 < coordR.m_element3)
                return -1;
            if (coordL.m_element3 > coordR.m_element3)
                return +1;

            return 0;
        }

        public VCoordinate4D Normal()
        {
            double scale = 1.0 / Length;
            return new VCoordinate4D(m_element0 * scale, m_element1 * scale, m_element2 * scale, m_element3 * scale);
        }

        public double Dot(VCoordinate4D coord)
        {
            return m_element0 * coord.m_element0 + m_element1 * coord.m_element1 + m_element2 * coord.m_element2 + m_element3 * coord.m_element3;
        }

        /// <summary>
        /// Creates a new coordinate that is of a specific dimension and fills it with data.
        /// </summary>
        /// <remarks>
        /// The resulting coordinate will be filled with coordinate data from this coordinate.
        /// </remarks>
        /// <param name="dimension">The dimension.</param>
        /// <returns></returns>
        public Coordinate ToDimension(int dimension)
        {
            Coordinate result = new Coordinate(dimension);

            if (dimension > 0)
                result[0] = m_element0;
            if (dimension > 1)
                result[1] = m_element1;
            if (dimension > 2)
                result[2] = m_element2;
            if (dimension > 3)
                result[3] = m_element3;

            return result;
        }

        public VCoordinate3D Lower()
        {
            return new VCoordinate3D(m_element0, m_element1, m_element2);
        }

        public VCoordinate4D HomoNormalise()
        {
            double scale = 1.0 / m_element3;
            return new VCoordinate4D(m_element0 * scale, m_element1 * scale, m_element2 * scale, 1.0);
        }

        public static VCoordinate4D Perpendicular3D(VCoordinate4D coordL, VCoordinate4D coordR)
        {
            return new VCoordinate4D(
                coordL.m_element1 * coordR.m_element2 - coordL.m_element2 * coordR.m_element1,
                coordL.m_element2 * coordR.m_element0 - coordL.m_element0 * coordR.m_element2,
                coordL.m_element0 * coordR.m_element1 - coordL.m_element1 * coordR.m_element0,
                coordL.m_element3);
        }

        public static VCoordinate4D Empty
        {
            get { return new VCoordinate4D(0.0, 0.0, 0.0, 0.0); }
        }

        public static bool operator ==(VCoordinate4D coordL, VCoordinate4D coordR)
        {
            if (coordL.m_element0 != coordR.m_element0)
                return false;
            if (coordL.m_element1 != coordR.m_element1)
                return false;
            if (coordL.m_element2 != coordR.m_element2)
                return false;
            if (coordL.m_element3 != coordR.m_element3)
                return false;

            return true;
        }

        public static bool operator !=(VCoordinate4D coordL, VCoordinate4D coordR)
        {
            if (coordL.m_element0 != coordR.m_element0)
                return true;
            if (coordL.m_element1 != coordR.m_element1)
                return true;
            if (coordL.m_element2 != coordR.m_element2)
                return true;
            if (coordL.m_element3 != coordR.m_element3)
                return true;

            return false;
        }

        public static VCoordinate4D operator +(VCoordinate4D coordL, VCoordinate4D coordR)
        {
            return new VCoordinate4D(coordL.m_element0 + coordR.m_element0, coordL.m_element1 + coordR.m_element1, coordL.m_element2 + coordR.m_element2, coordL.m_element3 + coordR.m_element3);
        }

        public static VCoordinate4D operator -(VCoordinate4D coordL, VCoordinate4D coordR)
        {
            return new VCoordinate4D(coordL.m_element0 - coordR.m_element0, coordL.m_element1 - coordR.m_element1, coordL.m_element2 - coordR.m_element2, coordL.m_element3 - coordR.m_element3);
        }

        public static VCoordinate4D operator -(VCoordinate4D coord)
        {
            return new VCoordinate4D(-coord.m_element0, -coord.m_element1, -coord.m_element2, -coord.m_element3);
        }

        public static VCoordinate4D operator *(VCoordinate4D coordL, VCoordinate4D coordR)
        {
            return new VCoordinate4D(coordL.m_element0 * coordR.m_element0, coordL.m_element1 * coordR.m_element1, coordL.m_element2 * coordR.m_element2, coordL.m_element3 * coordR.m_element3);
        }

        public static VCoordinate4D operator *(VCoordinate4D coord, double scale)
        {
            return new VCoordinate4D(coord.m_element0 * scale, coord.m_element1 * scale, coord.m_element2 * scale, coord.m_element3 * scale);
        }

        public static VCoordinate4D operator /(VCoordinate4D coordL, VCoordinate4D coordR)
        {
            return new VCoordinate4D(coordL.m_element0 / coordR.m_element0, coordL.m_element1 / coordR.m_element1, coordL.m_element2 / coordR.m_element2, coordL.m_element3 / coordR.m_element3);
        }

        public static VCoordinate4D operator /(VCoordinate4D coordL, double scale)
        {
            return new VCoordinate4D(coordL.m_element0 / scale, coordL.m_element1 / scale, coordL.m_element2 / scale, coordL.m_element3 / scale);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is VCoordinate4D))
                return false;

            VCoordinate4D coord = (VCoordinate4D)obj;
            return this == coord;
        }

        public bool Equals(VCoordinate4D coord)
        {
            return this == coord;
        }

        public override int GetHashCode()
        {
            return m_element0.GetHashCode() ^ m_element1.GetHashCode() ^ m_element2.GetHashCode() ^ m_element3.GetHashCode();
        }

        #endregion

        #region Private fields.

        private double m_element0;
        private double m_element1;
        private double m_element2;
        private double m_element3;

        #endregion

        #region Object level overrides.

        public override string ToString()
        {
            return string.Format("({0}, {1}, {2}, {3})", X, Y, Z, W);
        }

        #endregion

    }

}
