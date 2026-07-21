# MediaPipe 实现（预留）

本项目为 MediaPipe 原生实现的独立占位工程，本期不实现任何代码。

- 依赖：仅引用 `LingXuZhi.Vision.Abstractions`
- 切换方式：实现 `IHandDetector` / `IHandLandmarker` 后，在 `App/Hosting/AppHost.cs` 中将 DI 注册从 `LingXuZhi.Vision.OpenCv` 切换到本项目即可，上层无需改动
