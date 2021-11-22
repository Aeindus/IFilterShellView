using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace IFilterShellView.Extensions
{
    public static class ListViewItemExtensions
    {
        public static Rect GetListViewItemRect(this ListView listView, ListViewItem listViewItem)
        {
            Point transform = listViewItem.TransformToVisual((Visual)listView.Parent).Transform(new Point());
            Rect listViewItemBounds = VisualTreeHelper.GetDescendantBounds(listViewItem);
            listViewItemBounds.Offset(transform.X, transform.Y);

            return listViewItemBounds;
        }

        public static ListViewItem GetItemAt(this ListView listView, Point clientRelativePosition)
        {
            var hitTestResult = VisualTreeHelper.HitTest(listView, clientRelativePosition);
            if (hitTestResult == null) return null;

            var selectedItem = hitTestResult.VisualHit;
            while (selectedItem != null)
            {
                if (selectedItem is ListViewItem)
                {
                    break;
                }
                selectedItem = VisualTreeHelper.GetParent(selectedItem);
            }
            return selectedItem != null ? ((ListViewItem)selectedItem) : null;
        }
    }
}
