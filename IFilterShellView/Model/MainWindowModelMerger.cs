using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace IFilterShellView.Model
{
    public class MainWindowModelMerger
    {
        private readonly VisibilityModel _SearchPageVisibilityModel = new VisibilityModel();
        private readonly StringModel _SearchPageNoticeTitle = new StringModel();
        private readonly StringModel _SearchPageNoticeSubtitle = new StringModel();



        public VisibilityModel SearchPageVisibilityModel => _SearchPageVisibilityModel;
        public StringModel SearchPageNoticeTitle => _SearchPageNoticeTitle;
        public StringModel SearchPageNoticeMessage => _SearchPageNoticeSubtitle;
    }
}
