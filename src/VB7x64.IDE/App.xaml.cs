using System;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using VB7x64.IDE.Logging;

namespace VB7x64.IDE;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Registrar soporte para todas las páginas de códigos ANSI (Windows-1252, etc.)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        AppLogger.Info("=== Iniciando VB7-x64 IDE (Windows x64) ===");
        AppLogger.Info($"OS: {Environment.OSVersion}, .NET Runtime: {Environment.Version}, 64-bit Process: {Environment.Is64BitProcess}");

        // Configuración de captura global de excepciones
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("Excepción no controlada en hilo de interfaz (Dispatcher)", e.Exception);
        MessageBox.Show(
            $"Ocurrió un error inesperado en la interfaz:\n\n{e.Exception.Message}\n\nLos detalles se han registrado en el archivo de log.",
            "Error en VB7-x64 IDE",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Prevenir que la app se caiga abruptamente si es posible continuar
        e.Handled = true;
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            AppLogger.Error("Excepción no controlada en AppDomain", ex);
        }
        else
        {
            AppLogger.Error($"Excepción crítica no controlada: {e.ExceptionObject}");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.Error("Excepción no observada en tarea en segundo plano", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Info("=== Cerrando VB7-x64 IDE ===");
        base.OnExit(e);
    }
}
