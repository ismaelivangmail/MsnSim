using MsnSim.Data;

namespace MsnSim.Services;

/// <summary>
/// Hace de "servidor": cambia la presencia de los contactos, responde mensajes
/// y a veces inicia conversaciones. Todo corre en el hilo de UI (async/await),
/// así que los eventos llegan ya en el hilo principal.
/// Más adelante se puede sustituir por MsnClient.Core conectado a Escargot.
/// </summary>
public sealed class Simulador
{
    readonly Database _db;
    readonly Random _r = new();
    readonly HashSet<string> _pendientes = new();
    CancellationTokenSource? _cts;

    public event Action<Contacto, Estado>? PresenciaCambio;   // contacto actualizado, estado anterior
    public event Action<Mensaje, Contacto>? MensajeRecibido;
    public event Action<string, bool>? Escribiendo;           // email, sí/no

    public Simulador(Database db) => _db = db;

    public static bool EstaEnLinea(Estado e) => e is not (Estado.Desconectado or Estado.NoVisible);
    public static bool EstaEnLinea(Contacto c) => EstaEnLinea(c.Estado);

    CancellationToken Token => _cts?.Token ?? new CancellationToken(true);

    public void Iniciar()
    {
        Detener();
        _cts = new CancellationTokenSource();
        _ = BucleAsync(_cts.Token);
    }

    public void Detener()
    {
        _cts?.Cancel();
        _cts = null;
        _pendientes.Clear();
    }

    async Task BucleAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(8000, ct);
            while (!ct.IsCancellationRequested)
            {
                await TickAsync(ct);
                await Task.Delay(_r.Next(18000, 40000), ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    async Task TickAsync(CancellationToken ct)
    {
        var u = Servicios.Usuario;
        if (u is null) return;
        var contactos = await _db.ContactosAsync(u.Email);
        if (contactos.Count == 0) return;
        var c = contactos[_r.Next(contactos.Count)];

        // A veces un contacto en línea te escribe (si no estás "No visible").
        if (EstaEnLinea(c) && !c.Bloqueado && u.Estado != Estado.NoVisible
            && !_pendientes.Contains(c.Email) && _r.NextDouble() < 0.3)
        {
            await EscribirYEntregarAsync(c, Elegir(Iniciadores), ct);
            return;
        }

        var anterior = c.Estado;
        c.Estado = SiguienteEstado(anterior);
        if (_r.NextDouble() < 0.25) c.MensajePersonal = Elegir(MensajesPersonales);
        await _db.GuardarContactoAsync(c);
        if (!ct.IsCancellationRequested) PresenciaCambio?.Invoke(c, anterior);
    }

    Estado SiguienteEstado(Estado actual)
    {
        if (actual == Estado.Desconectado)
            return _r.NextDouble() < 0.7 ? Estado.Disponible : Estado.Ocupado;

        return _r.NextDouble() switch
        {
            < 0.30 => Estado.Desconectado,
            < 0.55 => Estado.Disponible,
            < 0.70 => Estado.Ocupado,
            < 0.82 => Estado.Ausente,
            < 0.90 => Estado.VuelvoEnseguida,
            < 0.95 => Estado.AlTelefono,
            _ => Estado.SaliAComer
        };
    }

    /// <summary>Llamar después de que el usuario envía un mensaje.</summary>
    public async Task ResponderAsync(Contacto c, string texto)
    {
        var ct = Token;
        if (c.Bloqueado || !EstaEnLinea(c) || !_pendientes.Add(c.Email)) return;
        try
        {
            bool ocupado = c.Estado != Estado.Disponible;
            if (ocupado && _r.NextDouble() < 0.4) return;   // los ocupados a veces no contestan
            await Task.Delay(ocupado ? _r.Next(4000, 9000) : _r.Next(900, 2500), ct);
            await EscribirYEntregarAsync(c, ElegirRespuesta(texto), ct);
        }
        catch (OperationCanceledException) { }
        finally { _pendientes.Remove(c.Email); }
    }

    /// <summary>Llamar después de que el usuario envía un zumbido.</summary>
    public async Task ZumbidoEnviadoAsync(Contacto c)
    {
        var ct = Token;
        if (c.Bloqueado || !EstaEnLinea(c) || !_pendientes.Add(c.Email)) return;
        try
        {
            await Task.Delay(_r.Next(1500, 3500), ct);
            if (_r.NextDouble() < 0.45) await EntregarAsync(c, "", true, ct);
            else await EscribirYEntregarAsync(c, Elegir(RespuestasZumbido), ct);
        }
        catch (OperationCanceledException) { }
        finally { _pendientes.Remove(c.Email); }
    }

    async Task EscribirYEntregarAsync(Contacto c, string texto, CancellationToken ct)
    {
        Escribiendo?.Invoke(c.Email, true);
        try { await Task.Delay(Math.Clamp(texto.Length * 110, 1300, 5000), ct); }
        finally { Escribiendo?.Invoke(c.Email, false); }
        await EntregarAsync(c, texto, false, ct);
    }

    async Task EntregarAsync(Contacto c, string texto, bool zumbido, CancellationToken ct)
    {
        var u = Servicios.Usuario;
        if (u is null || ct.IsCancellationRequested) return;
        var m = new Mensaje
        {
            Propietario = u.Email,
            Contacto = c.Email,
            Enviado = false,
            Texto = texto,
            EsZumbido = zumbido,
            Fecha = DateTime.Now,
            Leido = Servicios.ChatAbierto == c.Email
        };
        await _db.GuardarMensajeAsync(m);
        MensajeRecibido?.Invoke(m, c);
    }

    // ---------- Frases ----------

    string ElegirRespuesta(string texto)
    {
        var t = texto.ToLowerInvariant();
        if (Contiene(t, "hola", "qué onda", "que onda", "hey", "buenas", "quiubo")) return Elegir(Saludos);
        if (Contiene(t, "adiós", "adios", "bye", "me voy", "nos vemos", "bai")) return Elegir(Despedidas);
        if (Contiene(t, "jaja", "jeje", "xd", "lol")) return Elegir(Risas);
        if (t.Contains('?')) return Elegir(Preguntas);
        return Elegir(Generales);
    }

    static bool Contiene(string t, params string[] palabras) => palabras.Any(p => t.Contains(p));
    string Elegir(string[] a) => a[_r.Next(a.Length)];

    static readonly string[] Saludos = { "holaaa :D", "qué onda!", "hey! cómo estás?", "hola hola :)", "quiubo! qué haces?" };
    static readonly string[] Despedidas = { "byeee", "cuídate! :)", "ok, nos vemos al rato", "va, bye (L)" };
    static readonly string[] Risas = { "jajajaja", "XD", "jaja ya sé", "jajaja qué risa", "jeje :P" };
    static readonly string[] Preguntas = { "mmm no sé, tú qué opinas?", "jajaja sí obvio", "nel :P", "déjame pensarlo", "pues creo que sí", "neta no sé :S" };
    static readonly string[] Generales = { "jaja", "ah ok", "neta?", "qué buena onda (Y)", "ya vi", "ok ok", "jaja simón", "oye y qué haces?", "brb voy por un refresco", "va :)" };
    static readonly string[] Iniciadores = { "oye!", "estás ahí?", "ya viste mi mensaje personal? :P", "jaja me acordé de ti", "qué haces?", "ya te conectaste! :D" };
    static readonly string[] RespuestasZumbido = { "ya ya aquí estoy XD", "deja de zumbarme jaja", "qué pasó?? :O", "ahí voy, ahí voy" };
    static readonly string[] MensajesPersonales = { "(8) la de siempre (8)", "en clases :(", "tengo sueño", "alguien pa' jugar?", "feliz :D", "", "brb", "no molestar" };
}
