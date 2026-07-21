using System.Runtime.InteropServices;

namespace LingXuZhi.Platform.Mouse;

/// <summary>Win32 SendInput 鼠标控制。绝对移动使用 0~65535 归一化屏幕坐标。</summary>
public sealed class WindowsSendInputMouseController : IMouseController
{
    private const uint InputMouse = 0;
    private const uint MouseeventfMove = 0x0001;
    private const uint MouseeventfLeftdown = 0x0002;
    private const uint MouseeventfLeftup = 0x0004;
    private const uint MouseeventfRightdown = 0x0008;
    private const uint MouseeventfRightup = 0x0010;
    private const uint MouseeventfWheel = 0x0800;
    private const uint MouseeventfAbsolute = 0x8000;
    private const uint MouseeventfVirtualdesk = 0x4000;
    private const int WheelDelta = 120;

    public void MoveTo(float x, float y)
    {
        var (vx, vy) = ToAbsolute(x, y);
        Send(MouseeventfAbsolute | MouseeventfVirtualdesk | MouseeventfMove, vx, vy, 0);
    }

    public void MoveBy(int dx, int dy)
        => Send(MouseeventfMove, dx, dy, 0);

    public void LeftClick()
    {
        Send(MouseeventfLeftdown, 0, 0, 0);
        Send(MouseeventfLeftup, 0, 0, 0);
    }

    public void RightClick()
    {
        Send(MouseeventfRightdown, 0, 0, 0);
        Send(MouseeventfRightup, 0, 0, 0);
    }

    public void Scroll(int lines)
    {
        if (lines == 0)
            return;
        Send(MouseeventfWheel, 0, 0, lines * WheelDelta);
    }

    private static (int X, int Y) ToAbsolute(float pixelX, float pixelY)
    {
        var screenW = Math.Max(1, GetSystemMetrics(0)); // SM_CXSCREEN
        var screenH = Math.Max(1, GetSystemMetrics(1)); // SM_CYSCREEN
        var ax = (int)Math.Clamp(pixelX / (screenW - 1) * 65535.0, 0, 65535);
        var ay = (int)Math.Clamp(pixelY / (screenH - 1) * 65535.0, 0, 65535);
        return (ax, ay);
    }

    private static void Send(uint flags, int dx, int dy, int data)
    {
        var input = new Input
        {
            Type = InputMouse,
            Union = new InputUnion
            {
                Mouse = new MouseInput
                {
                    Dx = dx,
                    Dy = dy,
                    MouseData = data,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = nint.Zero,
                },
            },
        };
        _ = SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public int MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}
