using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Fragaria.Services;

namespace Fragaria;

public partial class App : Application, IXamlMetadataProvider
{
    private IXamlMetadataProvider? _xamlMetaDataProvider;

    private IXamlMetadataProvider XamlMetaDataProvider =>
        _xamlMetaDataProvider ??= CreateXamlMetaDataProvider();

    public App()
    {
        UnhandledException += (_, e) =>
            AppLogger.Error($"Unhandled UI exception (0x{e.Exception.HResult:X8})", e.Exception);
        InitializeComponent();
    }

    public IXamlType GetXamlType(Type type) => XamlMetaDataProvider.GetXamlType(type);

    public IXamlType GetXamlType(string fullName) => XamlMetaDataProvider.GetXamlType(fullName);

    public XmlnsDefinition[] GetXmlnsDefinitions() => XamlMetaDataProvider.GetXmlnsDefinitions();

    private static IXamlMetadataProvider CreateXamlMetaDataProvider()
    {
        var type = typeof(App).Assembly.GetType("Fragaria.Fragaria_XamlTypeInfo.XamlMetaDataProvider")
            ?? throw new InvalidOperationException("XamlMetaDataProvider was not generated.");
        return (IXamlMetadataProvider)Activator.CreateInstance(type)!;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var window = new MainWindow();
            window.Activate();
            AppLogger.Info("MainWindow activated");
        }
        catch (Exception ex)
        {
            AppLogger.Error("OnLaunched failed", ex);
            NativeDialog.ShowFatal(
                "Fragaria",
                $"Не удалось открыть окно: {ex.Message}\n\n{AppLogger.LogFilePath}");
        }
    }
}
