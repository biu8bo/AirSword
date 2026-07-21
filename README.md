# 凌虚指 / AirSword

> 隔空御剑，剑指为鼠 —— 基于电脑摄像头的手指剑指手势鼠标仿真工具

## 项目定位

利用笔记本前置摄像头识别"剑指"手势（食指与中指并拢伸直，其余手指弯曲收拢），实现隔空鼠标操作：移动、左键、右键、滚轮。技术栈 **.NET 8 + WinUI3**，手部识别基于 **OpenCV DNN + ONNX**（双模型：手掌检测 + 21 关键点），视觉识别层接口化设计，后续可切换 MediaPipe。

## 英文名说明

| 候选 | 含义 | 评价 |
|------|------|------|
| **AirSword** ✅ 推荐 | Air（隔空）+ Sword（剑指） | 简洁、好记、发音好、易做 logo，语义双关到位 |
| PhantomPoint | 虚影 + 指点 | 偏虚幻风，不够"剑" |
| VoidCursor | 虚空 + 光标 | 偏游戏风，缺手指意象 |

> 仓库 / 命名空间 / 解决方案统一用 `LingXuZhi`，对外品牌名 `AirSword`。

## 技术栈

| 层 | 选型 | 说明 |
|----|------|------|
| 运行时 | .NET 8 | LTS |
| UI | WinUI 3 (Windows App SDK) | 现代化桌面 UI |
| MVVM | CommunityToolkit.Mvvm | 官方推荐，源生成器 |
| 视觉 | OpenCvSharp4 + ONNX Runtime | DNN 推理 |
| 摄像头 | OpenCV VideoCapture / MediaFoundation | 可切换 |
| 鼠标 | Windows SendInput (P/Invoke) | 系统级仿真 |
| DI | Autofac | 容器随主窗口关闭 Dispose，自动释放 IDisposable 单例（原定 MS.DI，阶段 1 实施时变更） |

## 工程拆分（核心设计）

```
LingXuZhi.sln
├── src/
│   ├── LingXuZhi.App/                  # WinUI3 主应用
│   │   ├── ViewModels/                 # MVVM ViewModel
│   │   ├── Views/                      # 页面/窗口
│   │   ├── Controls/                   # 摄像头预览、骨架叠加、调试面板等自定义控件
│   │   ├── Hosting/                    # DI 容器装配、主机生命周期
│   │   └── Converters/                 # 值转换器
│   │
│   ├── LingXuZhi.Core/                 # 核心业务（零平台依赖，可单测）
│   │   ├── Gestures/                   # 手势识别 + 状态机
│   │   ├── Tracking/                   # 平滑滤波、死区、坐标映射
│   │   ├── Pipeline/                   # 处理管线编排（帧 → 识别 → 追踪 → 动作）
│   │   └── Configuration/             # 参数模型（灵敏度、死区、平滑系数等）
│   │
│   ├── LingXuZhi.Vision.Abstractions/  # 视觉识别接口与数据结构（零实现依赖）
│   │   └── IHandDetector / IHandLandmarker / ImageFrame / PalmDetection …
│   │
│   ├── LingXuZhi.Vision.OpenCv/       # OpenCV DNN ONNX 实现（独立项目）
│   │   ├── OpenCvPalmDetector / OpenCvHandLandmarker / RoiExtractor
│   │   └── Models/                    # ONNX 模型文件（随项目输出拷贝）
│   │
│   ├── LingXuZhi.Vision.MediaPipe/    # MediaPipe 实现预留（独立项目，本期仅 README）
│   │
│   └── LingXuZhi.Platform/           # 平台交互（Windows 实现）
│       ├── Camera/                    # ICameraSource + OpenCV/MF 实现
│       └── Mouse/                     # IMouseController + SendInput 实现
│
├── tests/
│   ├── LingXuZhi.Core.Tests/
│   └── LingXuZhi.Vision.Tests/
│
└── docs/
    ├── README.md                      # 本文件
    └── prompts/                       # 三阶段开发提示词
        ├── 01-skeleton-and-ui.md
        ├── 02-vision-and-visualization.md
        └── 03-tracking-and-mouse-simulation.md
```

## 关键解耦点

| 接口 | 职责 | 本期实现 | 预留切换 |
|------|------|----------|----------|
| `IHandDetector` | 输入帧 → 手掌边界框 + 关键点（旋转/缩放） | OpenCV DNN ONNX | MediaPipe Palm |
| `IHandLandmarker` | ROI 图像 → 21 个手部关键点坐标 | OpenCV DNN ONNX | MediaPipe Hand |
| `ICameraSource` | 摄像头帧采集（异步流） | OpenCV VideoCapture | MediaFoundation |
| `IMouseController` | 鼠标移动/左键/右键/滚轮 | Windows SendInput | — |
| `IGestureRecognizer` | 21 关键点 → 手势状态 | 剑指/捏合左/捏合右/张开滚轮/空闲 | — |

> **解耦红线**：`LingXuZhi.Core` 不得引用 OpenCvSharp、ONNX、WinUI、Windows API。所有平台/视觉依赖只能出现在 `Vision` / `Platform` / `App` 层。`Core` 只认自己定义的数据结构。

## 数据流（处理管线）

```
摄像头帧
  → IHandDetector          (手掌检测 → 边界框 + 旋转 ROI)
  → ROI 裁剪 + 仿射变换
  → IHandLandmarker        (21 关键点，归一化坐标)
  → 关键点反变换回原图坐标
  → Tracking 平滑滤波      (EMA / 卡尔曼，可配置)
  → 死区判定               (屏幕中央静止区不动作)
  → IGestureRecognizer     (状态机：剑指移动 / 捏合左键 / 捏合右键 / 张开滚轮 / 空闲)
  → IMouseController       (执行鼠标动作)
       │
       └──→ 可视化层       (骨架叠加 + 调试面板 + 状态指示)
```

## UI/UX 设计基线

来自 ui-ux-pro-max 设计系统查询，适配桌面调试工具：

| 维度 | 取值 | 理由 |
|------|------|------|
| 整体风格 | 深色技术调试风 | 长时间盯屏不累，凸显摄像头画面与骨架叠加 |
| 背景色 | `#0F172A` (Slate-900) | 深底 |
| 面板/卡片 | `#1E293B` (Slate-800) | 层次 |
| 主色（聚焦/激活） | `#3B82F6` (Blue-500) | 识别框、激活态 |
| 次要文本 | `#94A3B8` (Slate-400) | 调试数据 |
| 主文本 | `#F1F5F9` (Slate-100) | 高对比 |
| 警告/错误 | `#EF4444` / `#F59E0B` | 状态异常 |
| 成功/激活 | `#22C55E` | 识别成功 |
| 等宽字体 | JetBrains Mono | 坐标、FPS、调试数值 |
| UI 字体 | IBM Plex Sans（或系统默认 Segoe UI Variable） | 面板文字 |
| 过渡 | 150-300ms | 状态切换 |

> WinUI3 桌面应用优先用 Segoe UI Variable（系统原生），JetBrains Mono 用于调试数值区。颜色通过 `Application.Resources` 主题字典统一管理。

阶段 1 实施补充（详见 `prompts/01-skeleton-and-ui.md` 底部"第一阶段补充修改"）：

- 自定义标题栏替代系统标题栏（48px，含状态胶囊与 FPS），窗口按钮配色对齐深色主题。
- 底部运行日志面板（等宽字体、自动滚动、上限 500 行）。
- 窗口按工作区自适应（90% 居中，小屏最大化）；面板统一 1px 边框 `#334155`。

## 三阶段开发提示词索引

| 阶段 | 文档 | 目标 | 产出可验证标准 |
|------|------|------|----------------|
| 1 | `prompts/01-skeleton-and-ui.md` | 工程骨架 + 可视化界面 | 项目编译通过，摄像头预览运行，设置面板可用 |
| 2 | `prompts/02-vision-and-visualization.md` | 手部识别 + 骨架可视化 + 调试输出 | 摄像头画面上叠加 21 关键点骨架，调试面板实时显示坐标/FPS/置信度 |
| 3 | `prompts/03-tracking-and-mouse-simulation.md` | 手势追踪 + 鼠标仿真 | 剑指移动鼠标、捏合左右键、手掌滚轮，移动平滑、中央死区生效 |

## 设计原则（贯穿三阶段）

遵循 andrej-karpathy 行为准则：

1. **先想后写**：每个阶段开始前，AI 必须先列出假设、歧义点、备选方案，不确定就问，不默默选。
2. **最小实现**：只做该阶段要求的事，不跨阶段实现，不加未要求的"灵活性"。
3. **外科式修改**：跨阶段迭代时只改该阶段相关代码，不顺手重构无关代码。
4. **目标驱动**：每个阶段都有可验证的成功标准，做完即验证，不靠"感觉对了"。

## 模型来源

```bash
# 手掌检测
curl -LO https://github.com/opencv/opencv_zoo/raw/main/models/palm_detection_mediapipe/palm_detection_mediapipe_2023feb.onnx
# 21 关键点
curl -LO https://github.com/opencv/opencv_zoo/raw/main/models/handpose_estimation_mediapipe/handpose_estimation_mediapipe_2023feb.onnx
```

放入 `src/LingXuZhi.Vision.OpenCv/Models/`（OpenCV DNN 实现独立项目内，不放外部 assets 目录），构建时拷贝到输出目录 `Models\`（`CopyToOutputDirectory`）。
