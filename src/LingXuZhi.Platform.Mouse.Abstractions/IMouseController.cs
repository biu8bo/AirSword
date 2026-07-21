namespace LingXuZhi.Platform.Mouse;

/// <summary>鼠标控制抽象。实现可替换(SendInput / InputSimulator 等)。</summary>
public interface IMouseController
{
    void MoveTo(float x, float y);
    void MoveBy(int dx, int dy);
    void LeftDown();
    void LeftUp();
    void LeftClick();
    void DoubleClick();
    void RightClick();
    void Scroll(int lines);
}
