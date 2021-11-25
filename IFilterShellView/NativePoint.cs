using System.Runtime.InteropServices;

namespace IFilterShellView
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NativePoint
    {
        public NativePoint(int x, int y)
            : this()
        {
            X = x;
            Y = y;
        }

        public int X { get; set; }

        public int Y { get; set; }

        public static bool operator ==(NativePoint first, NativePoint second)
        {
            return first.X == second.X
                && first.Y == second.Y;
        }

        public static bool operator !=(NativePoint first, NativePoint second)
        {
            return !(first == second);
        }

        public override bool Equals(object obj)
        {
            return (obj != null && obj is NativePoint) ? this == (NativePoint)obj : false;
        }

        public override int GetHashCode()
        {
            int hash = X.GetHashCode();
            hash = hash * 31 + Y.GetHashCode();
            return hash;
        }
    }
}
