using Autofac;
using LingXuZhi.App.Services;
using LingXuZhi.App.ViewModels;
using LingXuZhi.Core.Configuration;
using LingXuZhi.Platform.Camera;
using LingXuZhi.Vision.Abstractions;
using LingXuZhi.Vision.OpenCv;

namespace LingXuZhi.App.Hosting;

/// <summary>Autofac 容器装配。容器随主窗口关闭而 Dispose,自动释放摄像头/模型等资源。</summary>
public static class AppHost
{
    public static IContainer Build()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<AppSettings>().SingleInstance();
        builder.RegisterType<OpenCvVideoCaptureSource>().As<ICameraSource>().SingleInstance();

        builder.RegisterInstance(VisionOptions.Default);
        builder.RegisterType<OpenCvPalmDetector>().As<IHandDetector>().SingleInstance();
        builder.RegisterType<OpenCvHandLandmarker>().As<IHandLandmarker>().SingleInstance();
        builder.RegisterType<HandTrackingService>().SingleInstance();

        builder.RegisterType<MainViewModel>().SingleInstance();

        return builder.Build();
    }
}
