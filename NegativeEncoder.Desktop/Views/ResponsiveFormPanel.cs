using Avalonia;
using Avalonia.Controls;

namespace NegativeEncoder.Desktop.Views;

// Reflows the same controls without reparenting them or losing edits/focus.
public sealed class ResponsiveFormPanel : Panel
{
    private const double ColumnGap = 12;
    private const double RowGap = 6;

    private static int Columns(double width) => width switch
    {
        double.PositiveInfinity => 1,
        >= 700 => 3,
        >= 350 => 2,
        _ => 1
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Children.Where(child => child.IsVisible).ToArray();
        var columns = Columns(availableSize.Width);
        var cellWidth = (availableSize.Width - (columns - 1) * ColumnGap) / columns;
        double height = 0, desiredWidth = 0;
        for (var row = 0; row < children.Length; row += columns)
        {
            double rowHeight = 0;
            for (var column = 0; column < columns && row + column < children.Length; column++)
            {
                var child = children[row + column];
                child.Measure(new Size(cellWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);
            }
            height += rowHeight + (row == 0 ? 0 : RowGap);
        }
        return new Size(desiredWidth * columns + (columns - 1) * ColumnGap, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children.Where(child => child.IsVisible).ToArray();
        var columns = Columns(finalSize.Width);
        var cellWidth = (finalSize.Width - (columns - 1) * ColumnGap) / columns;
        double y = 0;
        for (var row = 0; row < children.Length; row += columns)
        {
            var rowHeight = children.Skip(row).Take(columns).Max(child => child.DesiredSize.Height);
            for (var column = 0; column < columns && row + column < children.Length; column++)
                children[row + column].Arrange(new Rect(column * (cellWidth + ColumnGap), y, cellWidth, rowHeight));
            y += rowHeight + RowGap;
        }
        return finalSize;
    }
}
