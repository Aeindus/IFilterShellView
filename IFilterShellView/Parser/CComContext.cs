using System;

namespace IFilterShellView.Parser
{
    public class CComContext
    {

        public PidlAttributes PidlAttributesToConsider;
        public Sensitivity SearchSensitivity;


        public StringComparison StringComparisonEq => 
            SearchSensitivity == Sensitivity.CaseInsensitive ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;


        public CComContext() => SetToDefaultValues();


        public void SetToDefaultValues()
        {
            PidlAttributesToConsider = PidlAttributes.CreationTime;
            SearchSensitivity = Sensitivity.CaseInsensitive;
        }


        public enum Sensitivity
        {
            CaseSensitive,
            CaseInsensitive
        }
        public enum PidlAttributes
        {
            CreationTime,
            LastAccessTime,
            LastWriteTime
        }
    }
}
