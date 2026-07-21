# 阶段 1：工程骨架 + WinUI3 可视化界面

> 本阶段只搭骨架与界面，**不实现任何手部识别逻辑**。摄像头能出画面、设置面板能调参、调试区能显示占位数据即算完成。

## 阶段目标

搭建 `凌虚指 / AirSword` 的 .NET 8 + WinUI 3 工程骨架，完成可视化主界面，建立后续两阶段的承载框架。本阶段交付一个**能跑、能看、能调参、但不会识别手**的应用。

## 前置约束（必须遵守）

1. **技术栈固定**：.NET 8 + WinUI 3 (Windows App SDK) + CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection。
2. **工程拆分严格按 `docs/README.md`**：四个项目 `App / Core / Vision / Platform`，依赖方向单向（App → Core/Vision/Platform；Vision/Platform → Core；Core → 无）。
3. **Core 层零平台依赖**：`LingXuZhi.Core` 不得引用 OpenCvSharp、ONNX、WinUI、Windows API。本阶段 Core 可先放配置模型与管线接口骨架（空实现）。
4. **本阶段不写识别代码**：`Vision` 项目可建空接口 + 占位实现（返回空结果），`Platform.Camera` 要真实可跑（摄像头预览必须工作）。
5. **代码风格**：简洁优先，不做投机性抽象。单用途代码不抽接口。

## 本阶段范围

### 必做

- [ ] 解决方案与四个项目工程文件（.sln + .csproj），目标框架 `net8.0-windows10.0.19041.0`。
- [ ] NuGet 依赖：`Microsoft.WindowsAppSDK`、`Microsoft.Windows.SDK.BuildTools`、`CommunityToolkit.Mvvm`、`Microsoft.Extensions.DependencyInjection`、`Microsoft.Extensions.Hosting`、`OpenCvSharp4`、`OpenCvSharp4.runtime.win`、`Microsoft.ML.OnnxRuntime`。
- [ ] DI 容器装配（`Hosting/AppHost.cs`），注册 ViewModel、服务、配置。
- [ ] 主窗口（`Views/MainWindow.xaml`）+ 单页面布局（`Views/MainPage.xaml`）。
- [ ] **摄像头预览控件**（`Controls/CameraPreviewControl.xaml`）：实时显示摄像头帧，本阶段用 OpenCV `VideoCapture` 采集，转 SoftwareBitmap/WriteableBitmap 显示。
- [ ] **设置面板**（`Controls/SettingsPanelControl.xaml`）：摄像头设备选择、分辨率选择、灵敏度滑块、平滑系数滑块、死区半径滑块、启用鼠标仿真开关（本阶段开关无效，仅 UI）。
- [ ] **调试数据面板**（`Controls/DebugPanelControl.xaml`）：FPS、帧分辨率、处理耗时、状态文本、关键点坐标列表（本阶段为占位空数据）。
- [ ] **状态指示条**：当前手势状态（空闲/剑指/左键/右键/滚轮），本阶段恒显示"空闲"。
- [ ] `Core/Configuration/` 下参数模型（`AppSettings`：灵敏度、平滑系数、死区、镜像翻转等），绑定设置面板。
- [ ] 摄像头采集抽象 `ICameraSource`（定义在 `Platform.Camera`）+ OpenCV 实现。
- [ ] 应用生命周期：启动→初始化摄像头→预览；关闭→释放摄像头。

### 不做（留给后续阶段）

- 手部识别（阶段 2）
- 骨架叠加绘制（阶段 2）
- 真实调试数据（阶段 2）
- 手势状态机、鼠标仿真（阶段 3）

## 界面布局要求

主窗口采用三栏布局（参考调试工具常见结构）：

```
┌─────────────────────────────────────────────────────────────┐
│  顶部状态条：[凌虚指 AirSword]  [● 空闲]  [FPS 30]  [⚙ 设置]  │
├──────────────────────────────┬──────────────────────────────┤
│                              │  设置面板                     │
│   摄像头预览区               │  ├ 摄像头设备 [下拉]          │
│   （CameraPreviewControl）   │  ├ 分辨率     [下拉]          │
│   16:9，等比缩放，深色底     │  ├ 镜像翻转   [开关]          │
│   左下角叠加：分辨率/FPS      │  ├ 灵敏度     [滑块]          │
│                              │  ├ 平滑系数   [滑块]          │
│                              │  ├ 死区半径   [滑块]          │
│                              │  └ 启用鼠标仿真[开关]        │
│                              ├──────────────────────────────┤
│                              │  调试数据面板                 │
│                              │  ├ 状态：空闲                 │
│                              │  ├ 处理耗时：— ms             │
│                              │  ├ 手掌置信度：—              │
│                              │  └ 关键点坐标（21 行占位）    │
└──────────────────────────────┴──────────────────────────────┘
```

- 深色主题：背景 `#0F172A`，面板 `#1E293B`，主色 `#3B82F6`。
- 摄像头预览区是视觉焦点，占左侧 2/3 宽度；右侧 1/3 为设置 + 调试。
- 调试数据用等宽字体（JetBrains Mono 或 Cascadia Code）。
- 所有交互元素满足 44x44 触摸目标、可见焦点环、150-300ms 过渡。

## 实现步骤（建议顺序，每步可验证）

```
1. 建解决方案 + 四个 csproj，配置 TargetFramework 与 NuGet
   → 验证：dotnet build 通过
2. 写 App 启动骨架（App.xaml / App.xaml.cs），DI 容器装配
   → 验证：应用能启动显示空主窗口
3. 实现 ICameraSource 接口 + OpenCvVideoCaptureSource 实现
   → 验证：单元测试可采集一帧（或手动测试返回非空帧）
4. 实现 CameraPreviewControl（采集 → 转位图 → 显示）
   → 验证：主窗口能看到实时摄像头画面
5. 实现 AppSettings 配置模型 + 设置面板控件（双向绑定）
   → 验证：拖动滑块，绑定值实时变化（日志或断点确认）
6. 实现 DebugPanelControl（占位数据）
   → 验证：面板显示占位文本与空关键点列表
7. 顶部状态条 + 状态指示
   → 验证：显示"空闲"，FPS 实时更新
8. 摄像头设备/分辨率下拉枚举（OpenCV 枚举设备）
   → 验证：下拉能列出本机摄像头，切换生效
9. 资源字典统一主题色 + 字体
   → 验证：UI 符合设计基线配色
```

## 成功标准（验证清单）

- [ ] `dotnet build` 零警告零错误（允许 NuGet 还原信息）。
- [ ] 应用启动后能实时显示前置摄像头画面，≥ 20 FPS。
- [ ] 设置面板所有控件可交互且双向绑定生效。
- [ ] 调试面板显示 FPS、帧分辨率，关键点列表为占位空行。
- [ ] 状态条显示"空闲"。
- [ ] 关闭窗口不泄漏（摄像头资源释放，任务取消）。
- [ ] 四个项目依赖方向正确，Core 无平台引用（用 `dotnet list package` 核查）。
- [ ] 代码符合简洁原则：无未使用的抽象、无死代码、无空文件夹。

## 交付物

- 完整可编译运行的解决方案。
- `assets/models/` 目录（模型文件本阶段可不放，阶段 2 再下载）。
- 本阶段不写额外文档，代码自解释。

## 给 AI 的执行指令

> 你是凌虚指项目的开发者。请按上述阶段 1 规格实现。开始编码前，先用 3-5 行列出你对以下歧义点的假设：(a) 摄像头默认分辨率与帧率；(b) 镜像翻转默认值；(c) 是否支持多摄像头热切换。若有与规格冲突的理解，先提出再写代码。实现时遵循最小实现原则，不跨阶段实现识别/鼠标逻辑。每完成一个验证步骤，简述验证结果。

## 第一阶段补充修改（实施后追加）

以下为第一阶段实施过程中确认的设计变更，均属阶段 1 范围：

1. **DI 容器改用 Autofac**：替代原规格中的 `Microsoft.Extensions.DependencyInjection`。容器在 `Hosting/AppHost.cs` 装配，随主窗口关闭 Dispose，自动释放摄像头等 `IDisposable` 单例。
2. **窗口尺寸按工作区自适应**：不写死窗口尺寸。启动时取当前显示器工作区，占 90% 并居中；工作区宽度 < 1500px 时自动最大化，避免高 DPI 缩放下窗口超出屏幕导致控件被裁切。
3. **"灵敏度"更名为"鼠标灵敏度"**：设置面板标签明确语义，对应 `AppSettings.Sensitivity`（阶段 3 用作指针移动倍率）。
4. **底部新增运行日志面板**：主页面第三行，固定高 170px。时间戳 + 等宽字体，`KeepLastItemInView` 自动滚动到最新，上限 500 行自动裁剪最旧行。记录内容：应用启动、设备探测、摄像头启动/切换、预览出画、镜像与鼠标仿真开关变更。
5. **自定义标题栏替代系统标题栏**：`ExtendsContentIntoTitleBar + SetTitleBar`，48px 高。左侧应用图标 + 标题，随后手势状态胶囊，右侧 FPS 显示（预留系统窗口按钮区域）。原独立"顶部状态条"合并入标题栏。系统窗口按钮配色对齐深色主题（透明底/Slate 前景/悬停 Slate-800）。
6. **UI 分层强化（ui-ux-pro-max）**：所有面板加 1px 边框 `#334155`（PanelBorderBrush），日志区使用更深的 Muted 底色 `#272F42` 与内容区区分。
7. **全局异常钩子**：App 挂接 XAML / AppDomain / TaskScheduler 三类未处理异常，写入程序目录 `crash.log`，便于排查静默退出。
