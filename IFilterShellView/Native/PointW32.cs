using System.Runtime.InteropServices;

namespace IFilterShellView.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PointW32
    {
        public PointW32(int x, int y)
            : this()
        {
            X = x;
            Y = y;
        }

        public int X { get; set; }

        public int Y { get; set; }

        public static bool operator ==(PointW32 first, PointW32 second)
        {
            return first.X == second.X
                && first.Y == second.Y;
        }

        public static bool operator !=(PointW32 first, PointW32 second)
        {
            return !(first == second);
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj is PointW32 w && this == w;
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(X, Y);
        }
    }
}
