using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace IFilterShellView.Model
{
    public class MainWindowModelMerger
    {
        public VisibilityModel _SearchPageVisibilityModel = new VisibilityModel();

        public VisibilityModel SearchPageVisibilityModel
        {
            get => _SearchPageVisibilityModel;
        }


    }
}
