using Autofac;
using LingXuZhi.App.ViewModels;
using LingXuZhi.Core.Configuration;
using LingXuZhi.Platform.Camera;
using LingXuZhi.Vision;
using LingXuZhi.Vision.Abstractions;

namespace LingXuZhi.App.Hosting;

/// <summary>Autofac 容器装配。容器随主窗口关闭而 Dispose,自动释放摄像头等资源。</summary>
public static class AppHost
{
    public static IContainer Build()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<AppSettings>().SingleInstance();
        builder.RegisterType<OpenCvVideoCaptureSource>().As<ICameraSource>().SingleInstance();

        // 阶段 1 占位实现,阶段 2 替换为 OpenCV DNN 版本
        builder.RegisterType<NullHandDetector>().As<IHandDetector>().SingleInstance();
        builder.RegisterType<NullHandLandmarker>().As<IHandLandmarker>().SingleInstance();

        builder.RegisterType<MainViewModel>().SingleInstance();

        return builder.Build();
    }
}
