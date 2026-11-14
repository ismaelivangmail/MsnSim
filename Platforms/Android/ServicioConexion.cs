using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using MsnSim.Services;

namespace MsnSim;

/// <summary>
/// Servicio en primer plano: mientras corre, Android no cierra la app al salir de ella,
/// así el sincronizador sigue recibiendo mensajes y mostrando notificaciones.
/// No hace trabajo propio; sólo mantiene vivo el proceso (con su notificación fija).
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeSpecialUse)]
public class ServicioConexion : Service
{
    public const int IdNotificacion = 7001;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var notificacion = Notificaciones.CrearPersistente(this);
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
            StartForeground(IdNotificacion, notificacion, ForegroundService.TypeSpecialUse);
        else
            StartForeground(IdNotificacion, notificacion);

        // Si Android mata el proceso completo no lo relanzamos: sin la app no hay sesión abierta.
        return StartCommandResult.NotSticky;
    }
}
