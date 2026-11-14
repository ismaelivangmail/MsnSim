using Microsoft.Maui.ApplicationModel;
using AndroidX.Core.App;
using MsnSim.Data;
using MsnSim.UI;

namespace MsnSim.Services;

/// <summary>Notificaciones de Android cuando llega algo con la app en segundo plano.</summary>
public static class Notificaciones
{
    const string CanalMensajes = "mensajes_v1";
    const string CanalZumbidos = "zumbidos_v1";
    const string CanalConexion = "conexion_v1";
    static bool _canalesListos;

    static Android.Content.Context Ctx => Android.App.Application.Context;

    public static int IdRecurso(string nombre, string tipo) =>
        Ctx.Resources?.GetIdentifier(nombre, tipo, Ctx.PackageName) ?? 0;

    static Android.Net.Uri? UriSonido(string nombre)
    {
        int id = IdRecurso(nombre, "raw");
        return id == 0 ? null : Android.Net.Uri.Parse($"android.resource://{Ctx.PackageName}/{id}");
    }

    /// <summary>Android 13+: pide permiso para mostrar notificaciones (una sola vez).</summary>
    public static async Task PedirPermisoAsync()
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
                await Permissions.CheckStatusAsync<Permissions.PostNotifications>() != PermissionStatus.Granted)
                await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
        catch { /* el usuario puede activarlas después en Ajustes */ }
    }

    static void CrearCanales()
    {
        if (_canalesListos) return;
        _canalesListos = true;
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        var nm = (Android.App.NotificationManager?)Ctx.GetSystemService(Android.Content.Context.NotificationService);
        if (nm is null) return;
        var atributos = new Android.Media.AudioAttributes.Builder()
            .SetUsage(Android.Media.AudioUsageKind.Notification)!
            .SetContentType(Android.Media.AudioContentType.Sonification)!
            .Build();

        var mensajes = new Android.App.NotificationChannel(CanalMensajes, "Mensajes", Android.App.NotificationImportance.High)
        {
            Description = "Mensajes nuevos de tus contactos"
        };
        mensajes.SetSound(UriSonido("mensaje"), atributos);
        mensajes.EnableVibration(true);

        var zumbidos = new Android.App.NotificationChannel(CanalZumbidos, "Zumbidos", Android.App.NotificationImportance.High)
        {
            Description = "Cuando un contacto te envía un zumbido"
        };
        zumbidos.SetSound(UriSonido("zumbido"), atributos);
        zumbidos.EnableVibration(true);
        zumbidos.SetVibrationPattern(new long[] { 0, 400, 120, 400, 120, 400 });

        nm.CreateNotificationChannel(mensajes);
        nm.CreateNotificationChannel(zumbidos);
    }

    public static void Mostrar(Contacto c, Mensaje m)
    {
        try
        {
            CrearCanales();
            var abrir = Ctx.PackageManager?.GetLaunchIntentForPackage(Ctx.PackageName!);
            if (abrir is null) return;
            abrir.SetFlags(Android.Content.ActivityFlags.SingleTop | Android.Content.ActivityFlags.ClearTop);
            var alTocar = Android.App.PendingIntent.GetActivity(Ctx, 0, abrir,
                Android.App.PendingIntentFlags.Immutable | Android.App.PendingIntentFlags.UpdateCurrent);

            var nombre = Tema.Emoticones(c.Nombre);
            var texto = m.EsZumbido ? "¡Te ha enviado un zumbido!" : Tema.Emoticones(m.Texto);

            var b = new NotificationCompat.Builder(Ctx, m.EsZumbido ? CanalZumbidos : CanalMensajes);
            b.SetSmallIcon(IdRecurso("ic_notificacion", "drawable"));
            b.SetContentTitle(nombre);
            b.SetContentText(texto);
            b.SetStyle(new NotificationCompat.BigTextStyle().BigText(texto));
            b.SetLargeIcon(IconoGrande(c));
            b.SetColor(unchecked((int)0xFF1D5FA8));
            b.SetAutoCancel(true);
            b.SetContentIntent(alTocar);
            b.SetPriority(NotificationCompat.PriorityHigh);
            b.SetCategory(NotificationCompat.CategoryMessage);
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
                b.SetSound(UriSonido(m.EsZumbido ? "zumbido" : "mensaje"));

            NotificationManagerCompat.From(Ctx).Notify(c.Email.GetHashCode() & 0x7fffffff, b.Build());
        }
        catch (Exception ex)
        {
            // Sin permiso de notificaciones o algo del sistema: no debe tumbar la app.
            System.Diagnostics.Debug.WriteLine($"Notificaciones: {ex}");
        }
    }

    public static void LimpiarTodas()
    {
        // Borra las de mensajes; la fija del servicio no se toca (Android no deja quitarla).
        try { NotificationManagerCompat.From(Ctx).CancelAll(); } catch { }
    }

    /// <summary>Notificación fija mientras la app está conectada en segundo plano.</summary>
    public static Android.App.Notification CrearPersistente(Android.Content.Context ctx)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26) &&
            ctx.GetSystemService(Android.Content.Context.NotificationService) is Android.App.NotificationManager nm)
        {
            var canal = new Android.App.NotificationChannel(CanalConexion, "Conexión en segundo plano",
                Android.App.NotificationImportance.Low)   // sin sonido ni ventanita
            {
                Description = "Mantiene MsnSim conectado para recibir mensajes con la app cerrada"
            };
            canal.SetShowBadge(false);
            nm.CreateNotificationChannel(canal);
        }

        var abrir = ctx.PackageManager?.GetLaunchIntentForPackage(ctx.PackageName!);
        abrir?.SetFlags(Android.Content.ActivityFlags.SingleTop | Android.Content.ActivityFlags.ClearTop);
        var alTocar = abrir is null ? null : Android.App.PendingIntent.GetActivity(ctx, 1, abrir,
            Android.App.PendingIntentFlags.Immutable | Android.App.PendingIntentFlags.UpdateCurrent);

        var b = new NotificationCompat.Builder(ctx, CanalConexion);
        b.SetSmallIcon(IdRecurso("ic_notificacion", "drawable"));
        b.SetContentTitle("MsnSim está conectado");
        b.SetContentText("Recibirás tus mensajes aunque cierres la app.");
        b.SetColor(unchecked((int)0xFF1D5FA8));
        b.SetOngoing(true);
        b.SetShowWhen(false);
        b.SetPriority(NotificationCompat.PriorityLow);
        if (alTocar is not null) b.SetContentIntent(alTocar);
        return b.Build()!;
    }

    /// <summary>Foto del contacto o su inicial sobre su color, en alta resolución.</summary>
    static Android.Graphics.Bitmap? IconoGrande(Contacto c)
    {
        int lado = (int)(128 * (Ctx.Resources?.DisplayMetrics?.Density ?? 2f));
        if (Avatares.Existe(c.Avatar) && Android.Graphics.BitmapFactory.DecodeFile(c.Avatar) is { } foto)
            return Android.Graphics.Bitmap.CreateScaledBitmap(foto, lado, lado, true);

        var bmp = Android.Graphics.Bitmap.CreateBitmap(lado, lado, Android.Graphics.Bitmap.Config.Argb8888!);
        if (bmp is null) return null;
        var lienzo = new Android.Graphics.Canvas(bmp);
        var color = Tema.ColorDe(c.Email);
        lienzo.DrawColor(Android.Graphics.Color.Argb(255, (int)(color.Red * 255), (int)(color.Green * 255), (int)(color.Blue * 255)));

        var letras = c.Nombre.Where(char.IsLetterOrDigit).ToArray();
        var inicial = letras.Length > 0 ? char.ToUpperInvariant(letras[0]).ToString() : "?";
        using var pincel = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            Color = Android.Graphics.Color.White,
            TextSize = lado * 0.5f,
            TextAlign = Android.Graphics.Paint.Align.Center
        };
        pincel.SetTypeface(Android.Graphics.Typeface.DefaultBold);
        float y = lado / 2f - (pincel.Descent() + pincel.Ascent()) / 2f;
        lienzo.DrawText(inicial, lado / 2f, y, pincel);
        return bmp;
    }
}
