using System;
using System.Collections.Generic;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace QPARKShot.Models;

public enum ToolType
{
    Select,    // crop / no annotation
    Freehand,
    Arrow,
    Rectangle,
    Text, Redact, Blur, Callout
}

public abstract class Annotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ColorHex { get; set; } = "#FFFFFF";
    public double StrokeWidth { get; set; } = 4.0;
    public Annotation Copy()
    {
        var copy = (Annotation)MemberwiseClone();
        if (copy is FreehandAnnotation freehand) freehand.Points = new(freehand.Points);
        return copy;
    }
}

public sealed class FreehandAnnotation : Annotation
{
    public List<Point> Points { get; set; } = new();
}

public sealed class ArrowAnnotation : Annotation
{
    public Point Start { get; set; }
    public Point End { get; set; }
}

public sealed class RectangleAnnotation : Annotation
{
    public Rect Rect { get; set; }
}

public sealed class TextAnnotation : Annotation
{
    public Point Position { get; set; }
    public string Text { get; set; } = "";
    public double FontSize { get; set; } = 18;
}

public sealed class AreaAnnotation : Annotation
{
    public Rect Rect { get; set; }
    public bool Blur { get; set; }
}
public sealed class CalloutAnnotation : Annotation
{
    public Point Position { get; set; }
    public int Number { get; set; }
}
