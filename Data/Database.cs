using System.Security.Cryptography;
using System.Text;
using SQLite;

namespace MsnSim.Data;

public sealed class Database
{
    public const string UsuarioDemo = "ismaelivan@msn.com";
    const string PasswordDemo = "123";

    readonly SQLiteAsyncConnection _db;
    Task? _init;

    public Database(string ruta)
    {
        _db = new SQLiteAsyncConnection(ruta,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    public Task InitAsync() => _init ??= InicializarAsync();

    async Task InicializarAsync()
    {
        await _db.CreateTableAsync<Usuario>();
        await _db.CreateTableAsync<Contacto>();
        await _db.CreateTableAsync<Mensaje>();
        if (await _db.FindAsync<Usuario>(UsuarioDemo) is null)
            await SembrarAsync();
    }

    async Task SembrarAsync()
    {
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        await _db.InsertAsync(new Usuario
        {
            Email = UsuarioDemo,
            Salt = salt,
            PasswordHash = Hash(PasswordDemo, salt),
            Nombre = "Ivanovsky",
            MensajePersonal = "Probando MsnSim (Y)",
            Estado = Estado.Disponible
        });

        await SembrarContactosAsync(UsuarioDemo);
        var demo = await _db.FindAsync<Usuario>(UsuarioDemo);
        demo.DemoSembrado = true;
        await _db.UpdateAsync(demo);
    }

    /// <summary>Contactos simulados y un poco de historial para que una cuenta nueva no esté vacía.</summary>
    async Task SembrarContactosAsync(string propietario)
    {
        Contacto C(string email, string nombre, string personal, Estado e) =>
            new() { Propietario = propietario, Email = email, Nombre = nombre, MensajePersonal = personal, Estado = e };

        await _db.InsertAllAsync(new[]
        {
            C("estradaespino@msn.com", "Estrada", "ya quedó el servidor? :D", Estado.Disponible),
            C("karla_23@hotmail.com", "~*~ Karla ~*~", "(8) escuchando música (8)", Estado.Ocupado),
            C("elchino.gamer@msn.com", "El Chino [AFK]", "¿quién se avienta unas partidas?", Estado.Ausente),
            C("memo_rocks@msn.com", "Memo (*)", "brb", Estado.VuelvoEnseguida),
            C("mama.ivan@hotmail.com", "Mamá", "¿Ya comiste?", Estado.AlTelefono),
            C("paty.lopez@hotmail.com", "Paty", "", Estado.Desconectado),
            C("sofi.mate@hotmail.com", "Sofía (tarea)", "examen el lunes :(", Estado.Desconectado),
        });

        var ayer = DateTime.Now.AddDays(-1);
        Mensaje M(string contacto, bool enviado, string texto, int min, bool leido = true) =>
            new() { Propietario = propietario, Contacto = contacto, Enviado = enviado, Texto = texto, Fecha = ayer.AddMinutes(min), Leido = leido };

        await _db.InsertAllAsync(new[]
        {
            M("estradaespino@msn.com", false, "qué onda! ya quedó el servidor?", 0),
            M("estradaespino@msn.com", true, "casi, ya pasó todas las pruebas :D", 1),
            M("estradaespino@msn.com", false, "jaja chido (Y)", 2),
            M("karla_23@hotmail.com", false, "oye pásame la tarea de mate porfa", 30, leido: false),
        });
    }

    /// <summary>
    /// Guarda en el teléfono una cuenta validada por el servidor. La contraseña se guarda
    /// con hash para poder entrar también sin internet.
    /// </summary>
    public async Task<Usuario> GuardarCuentaRemotaAsync(string email, string nombre, string mensajePersonal,
        string password, string? token, string? avatarRemoto)
    {
        await InitAsync();
        var u = await _db.FindAsync<Usuario>(email) ?? new Usuario { Email = email, Estado = Estado.Disponible };
        u.Salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        u.PasswordHash = Hash(password, u.Salt);
        u.Nombre = nombre;
        u.MensajePersonal = mensajePersonal;
        u.Token = token;
        if (u.AvatarRemoto != avatarRemoto)
        {
            // La foto cambió en el servidor: se descargará de nuevo.
            Services.Avatares.BorrarLocal(u.Avatar);
            u.Avatar = null;
            u.AvatarRemoto = avatarRemoto;
        }
        // Contactos de demostración: sólo la primera vez, para que una cuenta nueva no se vea vacía.
        if (!u.DemoSembrado)
        {
            if ((await ContactosAsync(email)).Count == 0)
                await SembrarContactosAsync(email);
            u.DemoSembrado = true;
        }
        await _db.InsertOrReplaceAsync(u);
        return u;
    }

    public static string Hash(string password, string salt) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(salt + ":" + password)));

    // ---------- Usuarios ----------

    public async Task<Usuario?> ValidarAsync(string email, string password)
    {
        await InitAsync();
        var u = await _db.FindAsync<Usuario>(email.Trim().ToLowerInvariant());
        if (u is null) return null;
        var esperado = Convert.FromHexString(u.PasswordHash);
        var recibido = Convert.FromHexString(Hash(password, u.Salt));
        return CryptographicOperations.FixedTimeEquals(esperado, recibido) ? u : null;
    }

    public Task GuardarUsuarioAsync(Usuario u) => _db.UpdateAsync(u);

    public async Task<Usuario?> UsuarioAsync(string email)
    {
        await InitAsync();
        return await _db.FindAsync<Usuario>(email);
    }

    // ---------- Contactos ----------

    public async Task<List<Contacto>> ContactosAsync(string propietario)
    {
        await InitAsync();
        return await _db.Table<Contacto>().Where(c => c.Propietario == propietario).ToListAsync();
    }

    public Task GuardarContactoAsync(Contacto c) => c.Id == 0 ? _db.InsertAsync(c) : _db.UpdateAsync(c);

    public async Task EliminarContactoAsync(Contacto c)
    {
        await _db.DeleteAsync(c);
        await BorrarHistorialAsync(c.Propietario, c.Email);
    }

    public async Task<Contacto?> ContactoPorEmailAsync(string propietario, string email) =>
        (await ContactosAsync(propietario)).FirstOrDefault(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase));

    /// <summary>Quita el contacto de la lista pero conserva el historial.</summary>
    public Task QuitarContactoAsync(Contacto c) => _db.DeleteAsync(c);

    /// <summary>Borra los contactos de demostración y sus conversaciones.</summary>
    public async Task EliminarContactosDemoAsync(string propietario)
    {
        await _db.ExecuteAsync(
            "DELETE FROM mensajes WHERE Propietario = ? AND Contacto IN (SELECT Email FROM contactos WHERE Propietario = ? AND EsReal = 0)",
            propietario, propietario);
        await _db.ExecuteAsync("DELETE FROM contactos WHERE Propietario = ? AND EsReal = 0", propietario);
    }

    // ---------- Mensajes ----------

    public Task GuardarMensajeAsync(Mensaje m) => _db.InsertAsync(m);

    public async Task<List<Mensaje>> HistorialAsync(string propietario, string contacto, int max = 200)
    {
        var lista = await _db.Table<Mensaje>()
            .Where(m => m.Propietario == propietario && m.Contacto == contacto)
            .OrderByDescending(m => m.Id)
            .Take(max)
            .ToListAsync();
        lista.Reverse();
        return lista;
    }

    public async Task<Dictionary<string, int>> NoLeidosAsync(string propietario)
    {
        var lista = await _db.Table<Mensaje>()
            .Where(m => m.Propietario == propietario && m.Enviado == false && m.Leido == false)
            .ToListAsync();
        return lista.GroupBy(m => m.Contacto).ToDictionary(g => g.Key, g => g.Count());
    }

    public Task MarcarLeidosAsync(string propietario, string contacto) =>
        _db.ExecuteAsync("UPDATE mensajes SET Leido = 1 WHERE Propietario = ? AND Contacto = ?", propietario, contacto);

    public Task BorrarHistorialAsync(string propietario, string contacto) =>
        _db.ExecuteAsync("DELETE FROM mensajes WHERE Propietario = ? AND Contacto = ?", propietario, contacto);
}
