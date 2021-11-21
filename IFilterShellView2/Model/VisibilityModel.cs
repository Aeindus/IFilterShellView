using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IFilterShellView2.Model
{
    public class VisibilityModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private bool _visible = false;

        public bool Visible
        {
            get => _visible;
            set
            {
             if (_visible == value) return;

                _visible = value;
                NotifyPropertyChanged();
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyname = null)
        {
            if (PropertyChanged == null) return;
            PropertyChanged(this, new PropertyChangedEventArgs(propertyname));
        }
    }
}
