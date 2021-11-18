using System;
using System.Collections.Generic;
using System.Text;

namespace IFilterShellView2.Filter
{
    public enum FilterSettingsFlags : uint
    {
        F_STARTSWITH = 1U,
        F_CONTAINS = 2U,
        F_ENDSWITH = 4U,
        F_REGEX = 8U
    }
}
