using UnityEngine;
using UnityEngine.UIElements;

namespace Chroma.UIToolkit.Utility
{
    public static class VisualElementExtensions
    {
        /// <summary>
        /// Returns <see cref="true"/> if <paramref name="point"/> is inside <see cref="VisualElement"/>'s <see cref="Rect"/>
        /// In comparison to default <see cref="VisualElement.ContainsPoint(Vector2)"/> it includes the border pixels.
        /// </summary>
        public static bool ContainsPointWithBorders(this VisualElement visualElement, Vector2 point)
        {
            Vector2 size = visualElement.worldBound.size;
            return point.x >= 0 && point.x <= size.x && point.y >= 0 && point.y <= size.y;
        }
    }
}
