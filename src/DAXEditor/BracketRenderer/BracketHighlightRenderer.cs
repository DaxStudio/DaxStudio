// This code was largely "borrowed" from http://edi.codeplex.com

namespace DAXEditorControl.BracketRenderer
{
  using System;
  using System.Collections.Generic;
  using System.Windows.Media;
  using ICSharpCode.AvalonEdit.Document;
  using ICSharpCode.AvalonEdit.Rendering;

  /// <summary>
  /// Highlight opening and closing brackets when when moving the carret in the text
  /// 
  /// Source: https://github.com/icsharpcode/SharpDevelop/blob/master/src/AddIns/DisplayBindings/AvalonEdit.AddIn/Src/BracketHighlightRenderer.cs
  /// </summary>
  public class BracketHighlightRenderer : IBackgroundRenderer
  {
    #region fields
    BracketSearchResult result;
    Pen borderPen;
    Brush backgroundBrush;
    readonly TextView textView;

    //public static readonly Color DefaultBackground = Color.FromArgb(100, 0, 0, 255);
    public static readonly Color DefaultBackground = Color.FromArgb(200, 255, 200, 0);
    public static readonly Color DefaultBorder = Color.FromArgb(128, 255, 0, 0);
    public static readonly Color InvalidBackground = Color.FromArgb(150, 255, 90, 90);

    public const string BracketHighlight = "Bracket highlight";
    #endregion fields

    #region constructor
    public BracketHighlightRenderer(TextView textView)
    {
      this.textView = textView ?? throw new ArgumentNullException(nameof(textView));

      this.textView.BackgroundRenderers.Add(this);
    }
    #endregion constructor

    #region methods
    public void SetHighlight(BracketSearchResult result)
    {
      if (this.result != result)
      {
        this.result = result;
        textView.InvalidateLayer(this.Layer);
      }
    }

    void UpdateColors(Color background, Color foreground)
    {
      this.borderPen = new Pen(new SolidColorBrush(foreground), 1);
      this.borderPen.Freeze();

      this.backgroundBrush = new SolidColorBrush(background);
      this.backgroundBrush.Freeze();
    }

    public KnownLayer Layer
    {
      get
      {
        return KnownLayer.Selection;
      }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
      //check arguments are valid
      if (textView == null) throw new ArgumentNullException(nameof(textView));
      if (drawingContext == null) throw new ArgumentNullException(nameof(drawingContext));

      if (this.result == null)
        return;

      BackgroundGeometryBuilder builder = new BackgroundGeometryBuilder
      {
        CornerRadius = 1,
        AlignToWholePixels = true,
        BorderThickness = 1
      };

      builder.AddSegment(textView, new TextSegment() { StartOffset = result.OpeningBracketOffset, Length = result.OpeningBracketLength });
      builder.CloseFigure(); // prevent connecting the two segments
      builder.AddSegment(textView, new TextSegment() { StartOffset = result.ClosingBracketOffset, Length = result.ClosingBracketLength });
    
      Geometry geometry = builder.CreateGeometry();
        
      //Transform highlightFixTransform = new TranslateTransform(0, 2);
      //geometry.Transform.Value.OffsetY = 2;

      if (borderPen == null)
        this.UpdateColors(DefaultBackground, DefaultBackground);
      if (result.ClosingBracketLength == 0)
          this.UpdateColors(InvalidBackground, InvalidBackground);
      else
          this.UpdateColors(DefaultBackground, DefaultBackground);

      if (geometry != null)
      {
        drawingContext.DrawGeometry(backgroundBrush, borderPen, geometry );
      }
    }

    internal static void ApplyCustomizationsToRendering(BracketHighlightRenderer renderer, IEnumerable<Color> customizations)
    {
      renderer.UpdateColors(DefaultBackground, DefaultBorder);

      foreach (Color color in customizations)
      {
        //if (color.Name == BracketHighlight) {
        renderer.UpdateColors(color, color);
        //					renderer.UpdateColors(color.Background ?? Colors.Blue, color.Foreground ?? Colors.Blue);
        // 'break;' is necessary because more specific customizations come first in the list
        // (language-specific customizations are first, followed by 'all languages' customizations)
        break;
        //}
      }
    }
    #endregion methods
  }
}
