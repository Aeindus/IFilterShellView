using System;
using System.Collections.Generic;
using System.Text;

namespace IFilterShellView.Extensions
{
    public static class DoubleExtensions
    {
        public static bool EpsEq(this double val, double cmp)
        {
            return Math.Abs(val - cmp) < 0.00001;
        }
    }
}
