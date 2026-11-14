using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace MsnSim.Services;

public sealed record InfoVersion(int Version, string Nombre, string Apk, string Notas, bool Obligatoria);

/// <summary>
/// Busca un APK nuevo en la carpeta "actualizaciones" del servidor (version.json),
/// lo descarga y abre el instalador de Android.
/// </summary>
public static class Actualizaciones
{
    const string Carpeta = "actualizaciones/";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    static bool _revisadoAlIniciar;

    /// <summary>Número de compilación instalado (ApplicationVersion).</summary>
    public static int VersionInstalada => int.TryParse(AppInfo.Current.BuildString, out var v) ? v : 0;
    public static string NombreInstalado => AppInfo.Current.VersionString;

    /// <summary>Devuelve la versión del servidor si es más nueva que la instalada; null si no hay.</summary>
    public static async Task<InfoVersion?> BuscarAsync()
    {
        try
        {
            // El parámetro evita que algún caché devuelva un version.json viejo.
            var url = ApiCuentas.UrlDe($"{Carpeta}version.json?t={DateTime.UtcNow.Ticks}");
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var r = doc.RootElement;
            int version = r.TryGetProperty("version", out var v) && v.TryGetInt32(out var n) ? n : 0;
            if (version <= VersionInstalada) return null;
            return new InfoVersion(
                version,
                r.TryGetProperty("nombre", out var nom) ? nom.GetString() ?? version.ToString() : version.ToString(),
                r.TryGetProperty("apk", out var apk) ? apk.GetString() ?? "MsnSim.apk" : "MsnSim.apk",
                r.TryGetProperty("notas", out var notas) ? notas.GetString() ?? "" : "",
                r.TryGetProperty("obligatoria", out var ob) && ob.ValueKind == JsonValueKind.True);
        }
        catch
        {
            return null;   // sin internet, sin carpeta o JSON mal escrito: simplemente no hay actualización
        }
    }

    /// <summary>Descarga el APK al caché de la app informando el avance (0 a 1).</summary>
    public static async Task<string> DescargarAsync(InfoVersion info, IProgress<double> avance, CancellationToken ct)
    {
        var destino = Path.Combine(FileSystem.CacheDirectory, "MsnSim-actualizacion.apk");
        using var respuesta = await Http.GetAsync(ApiCuentas.UrlDe(Carpeta + info.Apk), HttpCompletionOption.ResponseHeadersRead, ct);
        respuesta.EnsureSuccessStatusCode();
        long? total = respuesta.Content.Headers.ContentLength;

        await using var origen = await respuesta.Content.ReadAsStreamAsync(ct);
        await using var archivo = File.Create(destino);
        var buffer = new byte[81920];
        long leidos = 0;
        int n;
        while ((n = await origen.ReadAsync(buffer, ct)) > 0)
        {
            await archivo.WriteAsync(buffer.AsMemory(0, n), ct);
            leidos += n;
            if (total > 0) avance.Report((double)leidos / total.Value);
        }
        return destino;
    }

    /// <summary>Abre el instalador de Android con el APK descargado.</summary>
    public static Task InstalarAsync(string rutaApk) =>
        Launcher.Default.OpenAsync(new OpenFileRequest("Instalar actualización",
            new ReadOnlyFile(rutaApk, "application/vnd.android.package-archive")));

    /// <summary>Al abrir la app: una sola vez por arranque.</summary>
    public static async Task RevisarAlIniciarAsync(Page pagina)
    {
        if (_revisadoAlIniciar) return;
        _revisadoAlIniciar = true;
        var info = await BuscarAsync();
        if (info is not null) await OfrecerAsync(pagina, info);
    }

    public static async Task OfrecerAsync(Page pagina, InfoVersion info)
    {
        var mensaje = $"Tienes la versión {NombreInstalado}." +
                      (string.IsNullOrWhiteSpace(info.Notas) ? "" : $"\n\nNovedades:\n{info.Notas}");
        bool actualizar = info.Obligatoria
            ? await pagina.DisplayAlert($"Versión {info.Nombre} disponible", mensaje + "\n\nEsta actualización es necesaria para seguir usando la app.", "Actualizar", "Salir") 
            : await pagina.DisplayAlert($"Versión {info.Nombre} disponible", mensaje, "Actualizar", "Después");

        if (!actualizar)
        {
            if (info.Obligatoria) Application.Current?.Quit();
            return;
        }
        await pagina.Navigation.PushModalAsync(new UI.ActualizacionPage(info));
    }
}
