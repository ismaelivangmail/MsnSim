using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Storage;
using MsnSim.Data;

namespace MsnSim.Services;

/// <summary>
/// Mantiene al día los contactos reales: cada 3 s (15 s en segundo plano) manda mi estado
/// al servidor y recibe contactos, solicitudes, mensajes y quién me está escribiendo.
/// Corre en el hilo de la interfaz (async/await), igual que el simulador.
/// </summary>
public sealed class Sincronizador
{
    const int MsPrimerPlano = 3000;
    const int MsSegundoPlano = 15000;
    const int MsChatAbierto = 1500;      // con una conversación en pantalla: "escribiendo" casi al instante

    readonly Database _db;
    readonly HashSet<string> _escribiendo = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _invitacionesAvisadas = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, DateTime> _ultimoAvisoEscribiendo = new(StringComparer.OrdinalIgnoreCase);
    CancellationTokenSource? _cts;
    TaskCompletionSource? _despertar;
    List<Invitacion> _invitaciones = new();

    public Sincronizador(Database db) => _db = db;

    public bool EnSegundoPlano { get; set; }

    /// <summary>Mínimo entre zumbidos; lo manda el servidor (msnsim_config.php).</summary>
    public int SegundosZumbido { get; private set; } = 30;
    public IReadOnlyList<Invitacion> Invitaciones => _invitaciones;

    public void Iniciar()
    {
        Detener();
        _invitaciones = new();
        _invitacionesAvisadas.Clear();
        _cts = new CancellationTokenSource();
        _ = BucleAsync(_cts.Token);
    }

    public void Detener()
    {
        _cts?.Cancel();
        _cts = null;
        foreach (var email in _escribiendo) Eventos.EmitirEscribiendo(email, false);
        _escribiendo.Clear();
    }

    /// <summary>Sincroniza ya, sin esperar al siguiente turno (tras agregar, aceptar, etc.).</summary>
    public void Despertar() => _despertar?.TrySetResult();

    async Task BucleAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await SincronizarAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"Sincronizador: {ex}");
            }

            _despertar = new TaskCompletionSource();
            int espera = EnSegundoPlano ? MsSegundoPlano
                       : Servicios.ChatAbierto is not null ? MsChatAbierto
                       : MsPrimerPlano;
            await Task.WhenAny(Task.Delay(espera, ct), _despertar.Task);
        }
    }

    async Task SincronizarAsync(CancellationToken ct)
    {
        var u = Servicios.Usuario;
        if (u?.Token is not { } token) return;

        var claveAck = $"ack:{u.Email}";
        long ack = Preferences.Default.Get(claveAck, 0L);
        var r = await ApiCuentas.SincronizarAsync(token, (int)u.Estado, ack);
        if (ct.IsCancellationRequested || !ReferenceEquals(Servicios.Usuario, u)) return;

        if (r.SesionVencida)
        {
            Detener();
            Eventos.EmitirSesionVencida();
            return;
        }
        if (!r.Ok) return;   // sin conexión o error temporal: se reintenta en el siguiente turno

        var d = r.Datos;
        await AplicarContactosAsync(u, d.GetProperty("contactos"));
        AplicarInvitaciones(d.GetProperty("invitaciones"));
        long nuevoAck = await AplicarMensajesAsync(u, d.GetProperty("mensajes"), ack);
        if (nuevoAck != ack) Preferences.Default.Set(claveAck, nuevoAck);
        AplicarEscribiendo(d.GetProperty("escribiendo"));
        if (d.TryGetProperty("config", out var cfg) && cfg.TryGetProperty("segundos_zumbido", out var sz) && sz.TryGetInt32(out var seg))
            SegundosZumbido = seg;
    }

    async Task AplicarContactosAsync(Usuario u, JsonElement lista)
    {
        var locales = await _db.ContactosAsync(u.Email);
        var porEmail = locales.GroupBy(c => c.Email, StringComparer.OrdinalIgnoreCase)
                              .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool cambios = false;

        foreach (var j in lista.EnumerateArray())
        {
            var email = Texto(j, "email");
            if (email is null) continue;
            vistos.Add(email);

            var estado = (Estado)Entero(j, "estado");
            var relacion = Texto(j, "relacion") ?? "pendiente";
            var nombre = Texto(j, "nombre") ?? email;
            var mensaje = Texto(j, "mensaje_personal") ?? "";
            var avatar = Texto(j, "avatar");

            bool nuevo = !porEmail.TryGetValue(email, out var c);
            c ??= new Contacto { Propietario = u.Email, Email = email, Estado = Estado.Desconectado };
            var anterior = c.Estado;

            bool perfilCambio = nuevo || !c.EsReal || c.Nombre != nombre || c.MensajePersonal != mensaje || c.Relacion != relacion;
            c.EsReal = true;
            c.Nombre = nombre;
            c.MensajePersonal = mensaje;
            c.Relacion = relacion;
            c.Bloqueado = relacion == "bloqueado";
            c.Estado = estado;

            if (c.AvatarRemoto != avatar)
            {
                if (avatar is null)
                {
                    Avatares.BorrarLocal(c.Avatar);
                    c.Avatar = null;
                    c.AvatarRemoto = null;
                    perfilCambio = true;
                }
                else if (await Avatares.DescargarAsync(avatar, c.Avatar) is { } ruta)
                {
                    c.Avatar = ruta;
                    c.AvatarRemoto = avatar;   // sólo si se descargó; si no, se reintenta después
                    perfilCambio = true;
                }
            }

            if (perfilCambio || anterior != estado)
                await _db.GuardarContactoAsync(c);

            if (!nuevo && anterior != estado) Eventos.EmitirPresencia(c, anterior);
            if (perfilCambio) cambios = true;
        }

        // Los que ya no están en el servidor (se quitaron desde otro lado).
        foreach (var c in locales.Where(c => c.EsReal && !vistos.Contains(c.Email)))
        {
            await _db.QuitarContactoAsync(c);
            cambios = true;
        }

        if (cambios) Eventos.EmitirContactosCambiaron();
    }

    void AplicarInvitaciones(JsonElement lista)
    {
        var nuevas = lista.EnumerateArray()
            .Select(j => (email: Texto(j, "email"), nombre: Texto(j, "nombre")))
            .Where(x => x.email is not null)
            .Select(x => new Invitacion(x.email!, x.nombre ?? x.email!))
            .ToList();

        bool cambio = nuevas.Count != _invitaciones.Count ||
                      nuevas.Any(n => !_invitaciones.Any(i => i.Email.Equals(n.Email, StringComparison.OrdinalIgnoreCase)));
        _invitaciones = nuevas;

        foreach (var i in nuevas)
            if (_invitacionesAvisadas.Add(i.Email))
                Eventos.EmitirInvitacion(i);
        if (cambio) Eventos.EmitirContactosCambiaron();
    }

    async Task<long> AplicarMensajesAsync(Usuario u, JsonElement lista, long ack)
    {
        long max = ack;
        List<Contacto>? contactos = null;

        foreach (var j in lista.EnumerateArray())
        {
            long id = j.TryGetProperty("id", out var v) && v.TryGetInt64(out var n) ? n : 0;
            if (id <= ack) continue;   // ya guardado en una sincronización anterior
            max = Math.Max(max, id);

            var de = Texto(j, "de");
            contactos ??= await _db.ContactosAsync(u.Email);
            var c = contactos.FirstOrDefault(x => string.Equals(x.Email, de, StringComparison.OrdinalIgnoreCase));
            if (de is null || c is null) continue;

            var m = new Mensaje
            {
                Propietario = u.Email,
                Contacto = c.Email,
                Enviado = false,
                Texto = Texto(j, "texto") ?? "",
                EsZumbido = Entero(j, "tipo") == 1,
                Fecha = FechaLocal(Texto(j, "creado")),
                Leido = Servicios.ChatAbierto == c.Email
            };
            await _db.GuardarMensajeAsync(m);

            if (_escribiendo.Remove(c.Email)) Eventos.EmitirEscribiendo(c.Email, false);
            Eventos.EmitirMensaje(m, c);
        }
        return max;
    }

    void AplicarEscribiendo(JsonElement lista)
    {
        var ahora = lista.EnumerateArray()
            .Where(j => j.ValueKind == JsonValueKind.String)
            .Select(j => j.GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var email in ahora.Where(e => !_escribiendo.Contains(e)).ToList())
        {
            _escribiendo.Add(email);
            Eventos.EmitirEscribiendo(email, true);
        }
        foreach (var email in _escribiendo.Where(e => !ahora.Contains(e)).ToList())
        {
            _escribiendo.Remove(email);
            Eventos.EmitirEscribiendo(email, false);
        }
    }

    // ---------- Acciones ----------

    static string? Token => Servicios.Usuario?.Token;

    static readonly RespuestaApi SinSesion = new(false,
        "Entraste sin conexión. Cierra sesión y vuelve a entrar con internet para usar tus contactos reales.", default, 0);

    async Task<RespuestaApi> TrasCambio(Task<RespuestaApi> llamada)
    {
        var r = await llamada;
        if (r.SesionVencida) { Detener(); Eventos.EmitirSesionVencida(); }
        else Despertar();
        return r;
    }

    public Task<RespuestaApi> AgregarAsync(string email) =>
        Token is { } t ? TrasCambio(ApiCuentas.AgregarContactoAsync(t, email)) : Task.FromResult(SinSesion);

    public Task<RespuestaApi> ResponderInvitacionAsync(string email, string respuesta)
    {
        if (Token is not { } t) return Task.FromResult(SinSesion);
        _invitaciones = _invitaciones.Where(i => !i.Email.Equals(email, StringComparison.OrdinalIgnoreCase)).ToList();
        return TrasCambio(ApiCuentas.ResponderInvitacionAsync(t, email, respuesta));
    }

    public Task<RespuestaApi> EliminarAsync(string email) =>
        Token is { } t ? TrasCambio(ApiCuentas.EliminarContactoAsync(t, email)) : Task.FromResult(SinSesion);

    public Task<RespuestaApi> CambiarBloqueoAsync(string email, bool bloquear) =>
        Token is { } t ? TrasCambio(ApiCuentas.CambiarBloqueoAsync(t, email, bloquear)) : Task.FromResult(SinSesion);

    public Task<RespuestaApi> EnviarAsync(string para, string texto, bool zumbido) =>
        Token is { } t ? TrasCambio(ApiCuentas.EnviarMensajeAsync(t, para, texto, zumbido)) : Task.FromResult(SinSesion);

    /// <summary>Avisa "estoy escribiendo" como mucho cada 3 s por contacto.</summary>
    public void AvisarEscribiendo(string para)
    {
        if (Token is not { } t) return;
        var ahora = DateTime.UtcNow;
        if (_ultimoAvisoEscribiendo.TryGetValue(para, out var ultimo) && (ahora - ultimo).TotalSeconds < 3) return;
        _ultimoAvisoEscribiendo[para] = ahora;
        _ = ApiCuentas.EscribiendoAsync(t, para);
    }

    // ---------- JSON ----------

    static string? Texto(JsonElement j, string p) =>
        j.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static int Entero(JsonElement j, string p)
    {
        if (!j.TryGetProperty(p, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        return v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out n) ? n : 0;
    }

    static DateTime FechaLocal(string? utc) =>
        DateTime.TryParseExact(utc, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
            ? d.ToLocalTime()
            : DateTime.Now;
}
