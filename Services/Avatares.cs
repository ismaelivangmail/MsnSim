using Microsoft.Maui.Storage;

namespace MsnSim.Services;

/// <summary>Elegir, preparar, guardar y descargar fotos de perfil.</summary>
public static class Avatares
{
    const int Lado = 512;   // el servidor la deja en 256; se manda un poco más grande

    static string Carpeta
    {
        get
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "avatares");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Lee la foto, la endereza según EXIF (las fotos de cámara vienen giradas),
    /// la recorta en cuadrado y la reduce. Devuelve JPEG listo para subir.
    /// </summary>
    public static async Task<byte[]> PrepararAsync(FileResult foto)
    {
        byte[] original;
        await using (var s = await foto.OpenReadAsync())
        using (var ms = new MemoryStream())
        {
            await s.CopyToAsync(ms);
            original = ms.ToArray();
        }
        return await Task.Run(() => Procesar(original));
    }

    static byte[] Procesar(byte[] original)
    {
        var limites = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
        Android.Graphics.BitmapFactory.DecodeByteArray(original, 0, original.Length, limites);
        if (limites.OutWidth <= 0 || limites.OutHeight <= 0)
            throw new InvalidOperationException("El archivo no es una imagen válida.");

        // Decodificar ya reducida para no gastar memoria con fotos de 12 MP.
        int muestra = 1;
        int menor = Math.Min(limites.OutWidth, limites.OutHeight);
        while (menor / (muestra * 2) >= Lado) muestra *= 2;
        using var bmp = Android.Graphics.BitmapFactory.DecodeByteArray(original, 0, original.Length,
                            new Android.Graphics.BitmapFactory.Options { InSampleSize = muestra })
                        ?? throw new InvalidOperationException("No se pudo leer la imagen.");

        int giro = 0;
        try
        {
            using var ms = new MemoryStream(original);
            var exif = new Android.Media.ExifInterface(ms);
            giro = exif.GetAttributeInt(Android.Media.ExifInterface.TagOrientation, 1) switch
            {
                6 => 90,
                3 => 180,
                8 => 270,
                _ => 0
            };
        }
        catch { /* sin EXIF */ }

        int w = bmp.Width, h = bmp.Height, lado = Math.Min(w, h);
        float escala = Math.Min(1f, (float)Lado / lado);
        var m = new Android.Graphics.Matrix();
        m.PostScale(escala, escala);
        if (giro != 0) m.PostRotate(giro);

        using var cuadrado = Android.Graphics.Bitmap.CreateBitmap(bmp, (w - lado) / 2, (h - lado) / 2, lado, lado, m, true)!;
        using var salida = new MemoryStream();
        cuadrado.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 85, salida);
        return salida.ToArray();
    }

    /// <summary>Guarda la foto en el teléfono con un nombre nuevo y borra la anterior.</summary>
    public static async Task<string> GuardarLocalAsync(byte[] jpeg, string? anterior)
    {
        var ruta = Path.Combine(Carpeta, $"{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(ruta, jpeg);
        BorrarLocal(anterior);
        return ruta;
    }

    public static void BorrarLocal(string? ruta)
    {
        try
        {
            if (!string.IsNullOrEmpty(ruta) && ruta.StartsWith(Carpeta, StringComparison.Ordinal) && File.Exists(ruta))
                File.Delete(ruta);
        }
        catch { /* no importa si no se pudo borrar */ }
    }

    /// <summary>Descarga la foto del servidor. Devuelve la ruta local o null si falla.</summary>
    public static async Task<string?> DescargarAsync(string rutaRemota, string? anterior)
    {
        var bytes = await ApiCuentas.DescargarAsync(rutaRemota);
        return bytes is null ? null : await GuardarLocalAsync(bytes, anterior);
    }

    public static bool Existe(string? ruta) => !string.IsNullOrEmpty(ruta) && File.Exists(ruta);
}
