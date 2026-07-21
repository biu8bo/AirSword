namespace LingXuZhi.Core.Gestures;

public interface IGestureRecognizer
{
    GestureObservation Recognize(IReadOnlyList<Vec2> landmarksNormalized);
}
