# 凌虚指 · AirSword

<p align="center">
  <img src="assets/logo.png" alt="凌虚指 AirSword" width="160" />
</p>

<p align="center">
  <strong>隔空御剑，指尖为鼠</strong><br/>
  用笔记本摄像头识别手势，把「食指」变成鼠标
</p>

---

## 这是什么

**凌虚指（AirSword）** 是一款 Windows 桌面工具：前置摄像头捕捉手部 21 关键点，识别指向 / 捏合 / 张开等手势，经平滑与死区处理后，通过系统级鼠标仿真控制指针、单击、双击、拖拽与滚轮。

面向「手不想碰触控板 / 鼠标」的场景：演示讲解、沙发躺用、无键鼠临时操控等。

| 项 | 说明 |
|----|------|
| 平台 | Windows 10 / 11（x64） |
| 技术 | .NET 8 · WinUI 3 · OpenCV DNN（ONNX）· Autofac |
| 识别 | 手掌检测 + 21 关键点估计（OpenCV Zoo MediaPipe 风格模型） |
| 控制 | `IMouseController` 抽象，默认 Win32 `SendInput` 实现 |

仓库 / 命名空间：`LingXuZhi`　对外品牌：**AirSword**

---

## 快速开始

### 环境

- Windows 10 1809+ / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 可用的摄像头（建议前置）
- Visual Studio 2022（可选，带 WinUI 工作负载）

### 构建与运行

```bash
git clone https://github.com/biu8bo/LingXuZhi.git
cd LingXuZhi
dotnet build LingXuZhi.sln -c Debug -p:Platform=x64
dotnet run --project src/LingXuZhi.App/LingXuZhi.App.csproj -c Debug -p:Platform=x64
```

首次启动会枚举摄像头并打开预览。设置面板中确认 **「启用鼠标仿真」**（默认已开启），再按下方手势操作。

### 单元测试

```bash
dotnet test tests/LingXuZhi.Core.Tests/LingXuZhi.Core.Tests.csproj
```

### 发布单文件

```bash
dotnet publish src/LingXuZhi.App/LingXuZhi.App.csproj -c Release -r win-x64 -p:Platform=x64 ^
  -p:SelfContained=false -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true ^
  -o publish
```

产出 `publish/LingXuZhi.App.exe`（约 200MB,框架依赖:内含 WinAppSDK、OpenCV 原生库与 ONNX 模型,目标机器需预装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)）。首次启动会将原生库与模型自解压到临时目录,稍慢属正常现象。  
若想完全免依赖(拷来即用),把 `-p:SelfContained=false` 改为 `true` 即可,体积约 260MB。

---

## 手势使用说明

手势基于 **MediaPipe 式 21 关键点**。指针采样点为 **食指尖(8)**。  
识别经状态机 **去抖**（默认连续 2 帧）后才会切换，减少误触。

### 1. 食指指向 · 移动鼠标

| | |
|--|--|
| **手势** | 食指伸直、中指收起，其他手指不参与判定 |
| **动作** | 指针跟随食指尖平滑移动 |
| **提示** | 手在画面 **中央死区**（预览上的虚线圆）内静止时，鼠标 **不移动**；离开死区后才继续跟随。中指也伸直时不识别为移动，四指全伸直会进入滚轮手势 |

适合日常移动光标。默认镜像开启，前置摄像头下左右方向符合直觉。

### 2. 拇指–食指捏合 · 左键单击 / 拖拽

| | |
|--|--|
| **手势** | 拇指尖靠近食指尖（捏合距离低于阈值）；**其他手指姿态任意** |
| **单击** | 原地捏合后松开 → **立即** 发出一次左键单击 |
| **双击** | 连续两次单击即可（与普通鼠标相同，无专用双击手势） |
| **拖拽** | 捏住并移动超过拖拽阈值 → 按住左键拖拽,指针跟随；松开捏合即释放 |
| **提示** | 想单击就捏了原地松开；想拖拽就捏住直接移动。手出画时会自动补发释放,不会卡住按键 |

### 3. 比耶并拢 · 右键单击

| | |
|--|--|
| **手势** | 食指与中指伸直做比耶，再把两指尖并拢；**无名指、小指宜收起**（四指全开会进滚轮） |
| **动作** | 触发 **一次** 右键单击 |
| **提示** | 并拢不放不会连点；与左键互不干扰（左键只看拇食距离） |

### 4. 张开手掌 · 滚轮（拇指定方向）

| | |
|--|--|
| **手势** | 食/中/无名/小指伸直张开，由 **拇指** 决定方向 |
| **向上滚** | **五指张开**（拇指也张开） |
| **向下滚** | **四指张开**（拇指弯向掌心） |
| **动作** | 保持手势即持续滚动（约 160ms 一次），默认每次滚动 3 行 |
| **提示** | 无需摆动手掌,切换拇指开合即可直接换向 |

### 5. 空闲

以上均不满足时进入空闲，不产生鼠标动作（预览与调试数据仍可刷新）。

### 推荐练习流程

1. 打开应用，面对摄像头，手完整入画  
2. 伸出 **食指**，观察预览十字线与指针跟随  
3. 移入中央死区，确认指针停下  
4. **拇指–食指原地捏合** 左键；连续两次单击可当双击；捏住移动试拖拽  
5. **食中比耶后并拢** 右键  
6. **五指张开** 向上滚；**拇指弯向掌心** 向下滚  
7. 若不需要控鼠，关闭设置中的「启用鼠标仿真」（仅可视化）

### 可调参数（设置面板）

| 参数 | 作用 | 默认倾向 |
|------|------|----------|
| 镜像翻转 | 前置摄像头左右镜像 | 开 |
| 鼠标灵敏度 | 以画面中心为锚放大偏移 | 1.5 |
| 平滑系数 | 越大越稳（内部映射为更小的 EMA α） | 0.7 |
| 死区半径 | 画面中央静止区（像素） | 40 |
| 滚轮行数 | 每次触发滚动行数 | 3 |
| 启用鼠标仿真 | 总开关 | 开 |

阈值类参数（捏合阈值 0.18、拖拽启动阈值 0.05、去抖帧数 2、检测置信度 / NMS）暂不在面板中开放，使用内部默认值。

---

## 功能一览

- 摄像头预览 + 骨架 / 手部旋转框叠加  
- 指针防闪跳:短暂跟丢冻结不复位(宽限 6 帧),单帧大位移需连续 3 帧确认  
- 捏合锁定:捏合成形时指针锚定到弯指前的瞄准位置,点击不跑偏;锁定期间指针只随手整体移动(食指根),拖拽不受影响  
- 死区圆、指针十字、当前手势文字叠加  
- 调试面板：手势状态、状态转移、平滑前后坐标、死区命中、鼠标位移、点击/滚轮计数  
- 底部运行日志；全局异常写入 `crash.log` 并回显日志面板  
- 鼠标实现可替换（抽象 / SendInput 分项目）

---

## 项目结构

```
LingXuZhi/
├── assets/                              # 品牌资源（SVG 源、PNG、ICO）
│   ├── logo.svg
│   ├── logo.png
│   └── App.ico
├── prompts/                             # 分阶段开发提示词
├── src/
│   ├── LingXuZhi.App/                   # WinUI 3 主程序
│   │   ├── Assets/                      # 应用图标（运行时拷贝）
│   │   ├── Controls/                    # 预览叠加、设置、调试面板
│   │   ├── Diagnostics/                 # 全局异常捕获
│   │   ├── Hosting/                     # Autofac 装配
│   │   ├── Services/                    # 手部追踪、手势→鼠标桥接
│   │   ├── ViewModels/
│   │   └── Views/
│   ├── LingXuZhi.Core/                  # 手势 / 平滑 / 死区 / 管线（零平台依赖）
│   │   ├── Configuration/
│   │   ├── Gestures/
│   │   ├── Pipeline/
│   │   └── Tracking/
│   ├── LingXuZhi.Vision.Abstractions/   # 视觉接口与纯数据类型
│   ├── LingXuZhi.Vision.OpenCv/         # OpenCV DNN + ONNX 实现与模型
│   ├── LingXuZhi.Vision.MediaPipe/      # MediaPipe 预留占位
│   ├── LingXuZhi.Platform/              # 摄像头等平台能力
│   ├── LingXuZhi.Platform.Mouse.Abstractions/  # IMouseController
│   └── LingXuZhi.Platform.Mouse.SendInput/    # SendInput 实现
└── tests/
    └── LingXuZhi.Core.Tests/            # 手势、状态机、平滑等单测
```

### 关键分层

| 接口 | 职责 | 当前实现 | 可替换方向 |
|------|------|----------|------------|
| `IHandDetector` | 帧 → 手掌框 | OpenCvPalmDetector | MediaPipe Palm |
| `IHandLandmarker` | ROI → 21 点 | OpenCvHandLandmarker | MediaPipe Hand |
| `ICameraSource` | 采集帧流 | OpenCvVideoCaptureSource | MediaFoundation 等 |
| `IMouseController` | 移动 / 单击 / 双击 / 按住释放 / 滚轮 | WindowsSendInputMouseController | 其他模拟库 |
| `IGestureRecognizer` | 21 点 → 手势观测 | DefaultGestureRecognizer | 自定义规则 |

**红线**：`LingXuZhi.Core` 不引用 OpenCvSharp、ONNX、WinUI、Win32。换视觉后端或鼠标库只需改 DI 注册。

### 数据流

```
摄像头帧
  → ICameraSource
  → HandTrackingService（检测 + 关键点）
  → GestureControlService / GesturePipeline
       识别 → 捏合锁定 → 跳变防护 → EMA 平滑 → 死区 → 状态机 → MouseAction
  → IMouseController（若启用仿真）
  → UI 预览叠加 + 调试面板
```

---

## 品牌与图标

源文件在 `assets/`：

- `logo.svg` — 矢量原稿（指向手势 + 光标火花，深色 slate + 蓝 + 琥珀）  
- `logo.png` — 高清位图（README / 宣传）  
- `App.ico` — 多尺寸图标（16～256）  

应用侧：`ApplicationIcon`、任务栏 `AppWindow.SetIcon`、标题栏 Logo 图均已接入。

重新从 SVG 出图时，可用 [resvg](https://github.com/RazrFalcon/resvg) 等工具栅格化后再打 ICO。

---

## 开发阶段

| 阶段 | 文档 | 状态 |
|------|------|------|
| 1 | `prompts/01-skeleton-and-ui.md` | 完成 · 骨架与界面 |
| 2 | `prompts/02-vision-and-visualization.md` | 完成 · 识别与可视化 |
| 3 | `prompts/03-tracking-and-mouse-simulation.md` | 完成 · 手势追踪与鼠标仿真 |

---

## 已知限制

- 本版本 **不支持多手、惯用手切换、自定义手势**  
- 强逆光 / 手出画 / 遮挡会导致识别中断  
- 自包含 WinAppSDK 输出体积较大；构建会裁剪多余语言资源目录（保留 `zh-CN` / `en-us`）

---

## 许可证与贡献

开源仓库：<https://github.com/biu8bo/LingXuZhi>  

欢迎 Issue / PR。改动请尽量保持 Core 无平台依赖，并补齐相关单测。
