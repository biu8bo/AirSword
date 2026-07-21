# 阶段 2：手部识别 + 骨架可视化 + 调试数据输出

> 本阶段接入 OpenCV DNN ONNX 双模型，识别 21 个手部关键点，在摄像头画面上叠加骨架，调试面板输出真实数据。**不做手势判定与鼠标控制**。

## 阶段目标

实现视觉识别层（`LingXuZhi.Vision`）：定义 `IHandDetector` / `IHandLandmarker` 双接口，完成 OpenCV DNN ONNX 实现，在阶段 1 的预览控件上叠加手部骨架与边界框，调试面板实时输出关键点坐标、置信度、处理耗时。

## 前置约束（必须遵守）

1. **接口优先**：`Vision/Abstractions/` 定义 `IHandDetector`、`IHandLandmarker` 与数据结构（`PalmDetection`、`HandLandmark`、`Point2D` 等）。ONNX 实现在 `Vision/OpenCv/`，依赖接口不依赖具体类。
2. **解耦红线**：识别接口的数据结构放在 `Vision/Abstractions` 或 `Core`，**不得引用 OpenCvSharp 类型**（OpenCV 的 `Mat` 不能出现在接口签名里）。接口只传字节数组/`Mat` 的托管包装或自定义 `ImageFrame` 结构。
3. **模型加载**：ONNX 模型从 `assets/models/` 加载，构建时拷贝到输出目录。模型路径通过配置注入，不硬编码绝对路径。
4. **不实现 MediaPipe**：`Vision/MediaPipe/` 仅放一个 `README.md` 说明预留，不写代码。
5. **本阶段不做手势判定**：21 关键点输出即可，不判定剑指/捏合（阶段 3）。
6. **本阶段不做鼠标控制**：识别结果只用于可视化与调试输出。

## 模型说明

两个模型均来自 OpenCV Zoo（MediaPipe 移植版）：

| 模型 | 输入 | 输出 | 用途 |
|------|------|------|------|
| `palm_detection_mediapipe_2023feb.onnx` | 1×3×192×192 (RGB, 归一化) | 手掌边界框 + 7 关键点 + 置信度 | 检测手掌位置与旋转 |
| `handpose_estimation_mediapipe_2023feb.onnx` | 1×3×224×224 (RGB, 归一化) | 21 关键点 (归一化坐标) | 精确关键点 |

> 处理流程：整帧 → 手掌检测 → 取置信度最高的手掌 → 用 7 关键点计算旋转 ROI → 仿射变换裁剪 224×224 → 关键点检测 → 反变换回原图坐标。若一帧多手，本阶段只取置信度最高的一只手（阶段 3 再考虑多手/惯用手）。

## 本阶段范围

### 必做

- [ ] `Vision/Abstractions/`：
  - `IHandDetector`：`Task<PalmDetectionResult> DetectAsync(ImageFrame frame, CancellationToken ct)`
  - `IHandLandmarker`：`Task<HandLandmarkResult> DetectAsync(ImageFrame roi, CancellationToken ct)`
  - 数据结构：`ImageFrame`（字节数组 + 宽高 + 通道 + 像素格式）、`PalmDetection`（边界框 + 7 关键点 + 置信度 + 旋转角）、`HandLandmark`（21 个 `Point2D` + 每点置信度）、`Point2D`（x, y, 可见性）。
- [ ] `Vision/OpenCv/`：
  - `OpenCvPalmDetector`：加载手掌检测 ONNX，预处理（resize 192×192、归一化），DNN 推理，后处理（解码边界框、NMS、反归一化）。
  - `OpenCvHandLandmarker`：加载关键点 ONNX，预处理（仿射变换裁剪 224×224、归一化），推理，后处理（21 点反归一化回原图坐标）。
  - `RoiExtractor`：用手掌 7 关键点计算旋转矩形 → 仿射变换矩阵 → 裁剪。
- [ ] DI 注册：`IHandDetector → OpenCvPalmDetector`，`IHandLandmarker → OpenCvHandLandmarker`。
- [ ] `CameraPreviewControl` 升级：在预览画面上叠加绘制层（`Canvas` 或 `Image` 叠加）：
  - 手掌边界框（带旋转角的矩形）
  - 7 个手掌关键点（小圆点）
  - 21 个手部关键点（按 MediaPipe 拓扑连线绘制骨架）
  - 置信度文本
- [ ] `DebugPanelControl` 升级：实时显示
  - 手掌检测置信度、边界框坐标、旋转角
  - 21 关键点坐标列表（编号 + x + y + 可见性）
  - 处理耗时拆分（检测 ms / 关键点 ms / 总 ms）
  - FPS（采集 FPS vs 处理 FPS）
  - 识别状态（检测到/未检测到/多手警告）
- [ ] 配置项补充：模型路径、置信度阈值、NMS 阈值、是否绘制骨架、是否绘制边界框。
- [ ] 性能：识别在后台线程跑，不阻塞 UI 线程；采用 `Channel<T>` 或 `BlockingCollection` 做帧队列，丢帧策略（处理慢时跳帧，不堆积）。

### 不做

- 手势状态机、剑指/捏合判定（阶段 3）
- 鼠标控制（阶段 3）
- 平滑滤波、死区（阶段 3）
- MediaPipe 实现

## 骨架绘制规格

21 关键点按 MediaPipe 标准拓扑连线（手指连线 + 手掌轮廓）：

```
手指连线：
  拇指:  0-1-2-3-4
  食指:  0-5-6-7-8
  中指:  5-9-10-11-12
  无名指:9-13-14-15-16
  小指:  13-17-18-19-20
  手掌:  0-5, 5-9, 9-13, 13-17, 0-17
```

- 关键点：圆点，指尖（4,8,12,16,20）用主色 `#3B82F6` 加大，其余用 `#94A3B8`。
- 连线：1.5px，`#3B82F6` 半透明。
- 边界框：旋转矩形，`#22C55E` 虚线。
- 置信度文本：关键点字体 JetBrains Mono，`#F1F5F9`。

## 实现步骤

```
1. 下载两个 ONNX 模型到 assets/models/，配置 csproj 拷贝
   → 验证：构建后输出目录有模型文件
2. 定义 Vision/Abstractions 接口与数据结构
   → 验证：编译通过，接口无 OpenCvSharp 引用
3. 实现 OpenCvPalmDetector（预处理→推理→后处理→NMS）
   → 验证：单帧输入返回合理边界框（用一张手部测试图断言非空）
4. 实现 RoiExtractor（7 关键点→旋转 ROI→仿射裁剪 224×224）
   → 验证：裁剪图肉眼可见是正立的手
5. 实现 OpenCvHandLandmarker（裁剪图→推理→21 点反变换回原图）
   → 验证：21 点坐标落在手掌区域，肉眼对齐
6. 串联：帧→手掌检测→ROI→关键点，封装为 Pipeline 步骤
   → 验证：端到端跑通，输出 21 点
7. 预览控件叠加绘制层（边界框 + 7 点 + 21 点骨架）
   → 验证：画面上骨架跟随手移动
8. 调试面板真实数据绑定
   → 验证：置信度、坐标、耗时、FPS 实时刷新
9. 后台线程 + 帧队列 + 丢帧策略
   → 验证：UI 不卡顿，处理慢时跳帧不堆积
10. 配置项（置信度阈值、NMS、绘制开关）接入设置面板
    → 验证：调阈值生效，关绘制开关骨架消失
```

## 成功标准（验证清单）

- [ ] 摄像头画面上实时叠加 21 关键点骨架，跟随手部移动，肉眼对齐无明显偏移。
- [ ] 手掌边界框（带旋转）正确框住手掌。
- [ ] 调试面板实时显示：手掌置信度、21 点坐标、检测/关键点耗时、采集/处理 FPS。
- [ ] UI 线程不卡顿（手部移动时预览流畅，≥ 20 FPS 采集）。
- [ ] `Vision/Abstractions` 无 `OpenCvSharp` 引用（`dotnet list package` 核查）。
- [ ] 手离开画面时状态显示"未检测到"，骨架消失。
- [ ] 切换置信度阈值生效（调高后远距离手不识别）。
- [ ] `Vision/MediaPipe/` 仅有 README 占位，无代码。
- [ ] 模型路径可配置，不硬编码绝对路径。

## 给 AI 的执行指令

> 你是凌虚指项目的开发者。请按上述阶段 2 规格实现。开始前先用 3-5 行列出假设：(a) ImageFrame 像素格式（BGR/RGB）；(b) 模型输入归一化方式（0-1 还是 -1~1）；(c) NMS 阈值默认值；(d) 多手时是否取置信度最高。若模型输入归一化不确定，先用 WebFetch 查 OpenCV Zoo 仓库的模型说明，不要猜。实现时严格保持接口与实现解耦，OpenCvSharp 类型不外泄到 Abstractions。每完成一个验证步骤，简述验证结果。
