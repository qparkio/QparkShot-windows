using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace QPARKShot.Models;

public enum ToolType
{
    Select,    // crop / no annotation
    Freehand,
    Arrow,
    Rectangle,
    Text
}

/// <summary>
/// Editor annotation. Sum type via abstract record + variants.
/// Coordinates are in source-image pixel space.
/// </summary>
public abstract record Annotation(Guid Id, string ColorHex, double StrokeWidth);

public sealed record FreehandAnnotation(
    Guid Id,
    string ColorHex,
    double StrokeWidth,
    List<Point> Points
) : Annotation(Id, ColorHex, StrokeWidth);

public sealed record ArrowAnnotation(
    Guid Id,
    string ColorHex,
    double StrokeWidth,
    Point Start,
    Point End
) : Annotation(Id, ColorHex, StrokeWidth);

public sealed record RectangleAnnotation(
    Guid Id,
    string ColorHex,
    double StrokeWidth,
    Rect Rect
) : Annotation(Id, ColorHex, StrokeWidth);

public sealed record TextAnnotation(
    Guid Id,
    string ColorHex,
    double StrokeWidth,
    Point Position,
    string Text,
    double FontSize
) : Annotation(Id, ColorHex, StrokeWidth);
