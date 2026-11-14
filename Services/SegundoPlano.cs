using Microsoft.Maui.Storage;

namespace MsnSim.Services;

/// <summary>Enciende o apaga el servicio que mantiene la app conectada al salir de ella.</summary>
public static class SegundoPlano
{
    public static bool Activado
    {
        get => Preferences.Default.Get("segundo_plano", true);
        set
        {
            Preferences.Default.Set("segundo_plano", value);
            if (value && Servicios.Usuario is not null) Iniciar(); else Detener();
        }
    }

    static Android.Content.Context Ctx => Android.App.Application.Context;

    public static void Iniciar()
    {
        if (!Activado) return;
        try
        {
            var intent = new Android.Content.Intent(Ctx, typeof(ServicioConexion));
            if (OperatingSystem.IsAndroidVersionAtLeast(26)) Ctx.StartForegroundService(intent);
            else Ctx.StartService(intent);
        }
        catch (Exception ex)
        {
            // Android 12+ no deja iniciarlo desde segundo plano; se intentará al volver a la app.
            System.Diagnostics.Debug.WriteLine($"SegundoPlano: {ex.Message}");
        }
    }

    public static void Detener()
    {
        try { Ctx.StopService(new Android.Content.Intent(Ctx, typeof(ServicioConexion))); }
        catch { }
    }
}
