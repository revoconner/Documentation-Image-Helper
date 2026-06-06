namespace DocumentationImageHelper.Editor;

/// <summary>Identifies which editing tool is currently active.</summary>
public enum ToolType
{
    /// <summary>No tool selected; the canvas only pans and zooms.</summary>
    None,

    /// <summary>Click to place editable text on the image.</summary>
    Text,

    /// <summary>Freehand drawing, or a straight line when Shift is held.</summary>
    Stroke,

    /// <summary>Drag a corner-to-corner rectangle outline.</summary>
    Rectangle,

    /// <summary>Click the centre and drag outward; the drag distance is the radius.</summary>
    Circle,

    /// <summary>Drag a bounding box that produces an ellipse outline.</summary>
    Oval,

    /// <summary>Drag a region to crop the image down to.</summary>
    Crop
}
