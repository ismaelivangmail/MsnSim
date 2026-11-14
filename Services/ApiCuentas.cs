using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MsnSim.Services;

/// <param name="Http">Código HTTP; 0 = no hubo conexión con el servidor.</param>
public sealed record RespuestaApi(bool Ok, string? Error, JsonElement Datos, int Http)
{
    public bool SinConexion => Http == 0;
    public bool SesionVencida => Http == 401;

    public string? Texto(string propiedad) =>
        Datos.ValueKind == JsonValueKind.Object && Datos.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    public bool Booleano(string propiedad) =>
        Datos.ValueKind == JsonValueKind.Object && Datos.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.True;

    public string? TextoUsuario(string propiedad) =>
        Datos.ValueKind == JsonValueKind.Object && Datos.TryGetProperty("usuario", out var u) &&
        u.ValueKind == JsonValueKind.Object && u.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}

/// <summary>Cliente de la API de cuentas (api.php en tu hosting).</summary>
public static class ApiCuentas
{
    // Servidor de pruebas (XAMPP + No-IP). Cambiar a https cuando tenga certificado.
    public const string Url = "http://gn3nomina.ddns.net/api.php";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static Task<RespuestaApi> SolicitarCodigoAsync(string email) =>
        LlamarAsync(new JsonObject { ["accion"] = "solicitar_codigo", ["email"] = email });

    public static Task<RespuestaApi> VerificarCodigoAsync(string email, string codigo) =>
        LlamarAsync(new JsonObject { ["accion"] = "verificar_codigo", ["email"] = email, ["codigo"] = codigo });

    public static Task<RespuestaApi> CrearCuentaAsync(string email, string token, string password, string nombre) =>
        LlamarAsync(new JsonObject
        {
            ["accion"] = "crear_cuenta", ["email"] = email, ["token"] = token,
            ["password"] = password, ["nombre"] = nombre
        });

    public static Task<RespuestaApi> IniciarSesionAsync(string email, string password) =>
        LlamarAsync(new JsonObject { ["accion"] = "iniciar_sesion", ["email"] = email, ["password"] = password });

    public static Task<RespuestaApi> CerrarSesionAsync(string sesion) =>
        LlamarAsync(new JsonObject { ["accion"] = "cerrar_sesion", ["sesion"] = sesion });

    public static Task<RespuestaApi> ActualizarPerfilAsync(string sesion, string nombre, string mensajePersonal) =>
        LlamarAsync(new JsonObject
        {
            ["accion"] = "actualizar_perfil", ["sesion"] = sesion,
            ["nombre"] = nombre, ["mensaje_personal"] = mensajePersonal
        });

    public static Task<RespuestaApi> SubirAvatarAsync(string sesion, byte[] jpeg) =>
        LlamarAsync(new JsonObject
        {
            ["accion"] = "subir_avatar", ["sesion"] = sesion, ["imagen"] = Convert.ToBase64String(jpeg)
        });

    public static Task<RespuestaApi> QuitarAvatarAsync(string sesion) =>
        LlamarAsync(new JsonObject { ["accion"] = "quitar_avatar", ["sesion"] = sesion });

    // ---------- Contactos reales ----------

    public static Task<RespuestaApi> SincronizarAsync(string sesion, int estado, long ack) =>
        LlamarAsync(new JsonObject { ["accion"] = "sincronizar", ["sesion"] = sesion, ["estado"] = estado, ["ack"] = ack });

    public static Task<RespuestaApi> AgregarContactoAsync(string sesion, string email) =>
        LlamarAsync(new JsonObject { ["accion"] = "agregar_contacto", ["sesion"] = sesion, ["email"] = email });

    /// <param name="respuesta">aceptar | rechazar | bloquear</param>
    public static Task<RespuestaApi> ResponderInvitacionAsync(string sesion, string email, string respuesta) =>
        LlamarAsync(new JsonObject { ["accion"] = "responder_invitacion", ["sesion"] = sesion, ["email"] = email, ["respuesta"] = respuesta });

    public static Task<RespuestaApi> EliminarContactoAsync(string sesion, string email) =>
        LlamarAsync(new JsonObject { ["accion"] = "eliminar_contacto", ["sesion"] = sesion, ["email"] = email });

    public static Task<RespuestaApi> CambiarBloqueoAsync(string sesion, string email, bool bloquear) =>
        LlamarAsync(new JsonObject { ["accion"] = "cambiar_bloqueo", ["sesion"] = sesion, ["email"] = email, ["bloquear"] = bloquear });

    public static Task<RespuestaApi> EnviarMensajeAsync(string sesion, string para, string texto, bool zumbido) =>
        LlamarAsync(new JsonObject
        {
            ["accion"] = "enviar_mensaje", ["sesion"] = sesion, ["para"] = para,
            ["texto"] = texto, ["tipo"] = zumbido ? 1 : 0
        });

    public static Task<RespuestaApi> EscribiendoAsync(string sesion, string para) =>
        LlamarAsync(new JsonObject { ["accion"] = "escribiendo", ["sesion"] = sesion, ["para"] = para });

    /// <summary>Convierte una ruta relativa de la API (avatares/x.jpg) en dirección completa.</summary>
    public static string UrlDe(string rutaRelativa) => new Uri(new Uri(Url), rutaRelativa).ToString();

    public static async Task<byte[]?> DescargarAsync(string rutaRelativa)
    {
        try { return await Http.GetByteArrayAsync(UrlDe(rutaRelativa)); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException) { return null; }
    }

    // JSON armado y leído a mano (JsonObject/JsonDocument) para que funcione con el recorte de Release.
    static async Task<RespuestaApi> LlamarAsync(JsonObject cuerpo)
    {
        try
        {
            using var contenido = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json");
            using var r = await Http.PostAsync(Url, contenido);
            var texto = await r.Content.ReadAsStringAsync();

            JsonElement raiz;
            try
            {
                using var doc = JsonDocument.Parse(texto);
                raiz = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return new(false, $"El servidor respondió algo inesperado (HTTP {(int)r.StatusCode}). Revisa la dirección de la API.", default, (int)r.StatusCode);
            }

            bool ok = raiz.ValueKind == JsonValueKind.Object &&
                      raiz.TryGetProperty("ok", out var o) && o.ValueKind == JsonValueKind.True;
            var respuesta = new RespuestaApi(ok, null, raiz, (int)r.StatusCode);
            return ok ? respuesta : respuesta with { Error = respuesta.Texto("error") ?? $"Error del servidor (HTTP {(int)r.StatusCode})." };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, "No se pudo conectar con el servidor. Revisa tu conexión a internet.", default, 0);
        }
    }
}
