namespace MsnSim.Services;

/// <summary>
/// Sonidos cortos de la app (Platforms/Android/Resources/raw). Usa SoundPool como
/// "evento de notificación": respeta el modo silencio y el volumen de notificaciones.
/// </summary>
public static class Sonidos
{
    static Android.Media.SoundPool? _pool;
    static int _mensaje, _zumbido, _conectado;

    public static void Iniciar()
    {
        if (_pool is not null) return;
        try
        {
            var ctx = Android.App.Application.Context;
            var atributos = new Android.Media.AudioAttributes.Builder()
                .SetUsage(Android.Media.AudioUsageKind.NotificationEvent)!
                .SetContentType(Android.Media.AudioContentType.Sonification)!
                .Build();
            _pool = new Android.Media.SoundPool.Builder()
                .SetMaxStreams(3)!
                .SetAudioAttributes(atributos)!
                .Build();

            _mensaje = Cargar(ctx, "mensaje");
            _zumbido = Cargar(ctx, "zumbido");
            _conectado = Cargar(ctx, "conectado");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sonidos: {ex}");
            _pool = null;
        }
    }

    static int Cargar(Android.Content.Context ctx, string nombre)
    {
        int recurso = Notificaciones.IdRecurso(nombre, "raw");
        return recurso == 0 || _pool is null ? 0 : _pool.Load(ctx, recurso, 1);
    }

    public static void Mensaje() => Tocar(_mensaje);
    public static void Zumbido() => Tocar(_zumbido);
    public static void Conectado() => Tocar(_conectado);

    static void Tocar(int id)
    {
        if (!Alertas.SonidosActivados || _pool is null || id == 0) return;
        try { _pool.Play(id, 1f, 1f, 1, 0, 1f); }
        catch { /* sin audio disponible */ }
    }
}
