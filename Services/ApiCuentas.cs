using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MsnSim.Services;

public sealed record RespuestaApi(bool Ok, string? Error, JsonElement Datos)
{
    public string? Texto(string propiedad) =>
        Datos.ValueKind == JsonValueKind.Object && Datos.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    public string? TextoUsuario(string propiedad) =>
        Datos.ValueKind == JsonValueKind.Object && Datos.TryGetProperty("usuario", out var u) &&
        u.ValueKind == JsonValueKind.Object && u.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}

/// <summary>Cliente de la API de cuentas (api.php en tu hosting).</summary>
public static class ApiCuentas
{
    // ⚠️ Cambia esto por la dirección real de tu hosting (con https).
    public const string Url = "http://gn3nomina.ddns.net/api.php";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

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
                return new(false, $"El servidor respondió algo inesperado (HTTP {(int)r.StatusCode}). Revisa la dirección de la API.", default);
            }

            bool ok = raiz.ValueKind == JsonValueKind.Object &&
                      raiz.TryGetProperty("ok", out var o) && o.ValueKind == JsonValueKind.True;
            var respuesta = new RespuestaApi(ok, null, raiz);
            return ok ? respuesta : respuesta with { Error = respuesta.Texto("error") ?? $"Error del servidor (HTTP {(int)r.StatusCode})." };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, "No se pudo conectar con el servidor. Revisa tu conexión a internet.", default);
        }
    }
}
