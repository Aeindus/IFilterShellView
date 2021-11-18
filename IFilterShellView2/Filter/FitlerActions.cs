using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace IFilterShellView2.Filter
{
    public static class FitlerActions
    {
        public static readonly Dictionary<FilterSettingsFlags, Func<string, string, bool>> SettingsActionMap = new Dictionary<FilterSettingsFlags, Func<string, string, bool>>
        {
            { FilterSettingsFlags.F_STARTSWITH, (pidl_name, input) => pidl_name.StartsWith(input)},
            { FilterSettingsFlags.F_CONTAINS, (pidl_name, input) => pidl_name.Contains(input)},
            { FilterSettingsFlags.F_ENDSWITH, (pidl_name, input) => pidl_name.EndsWith(input)},
            { FilterSettingsFlags.F_REGEX, (pidl_name, input) => RegexFilterCallback(pidl_name, input)}
        };

        private static (string Input, Regex CompiledRegex) FilterRegexContainer = ("", null);

        public static bool RegexFilterCallback(string pidl_name, string input)
        {
            if (!FilterRegexContainer.Input.Equals(input))
            {
                FilterRegexContainer.CompiledRegex = new Regex(input, RegexOptions.Compiled);
                FilterRegexContainer.Input = input;
            }

            return FilterRegexContainer.Item2.Match(pidl_name).Success;
        }

    }
}
