using Microsoft.Maui.Storage;
using MsnSim.Data;

namespace MsnSim.Services;

/// <summary>Estado global sencillo de la app (sin inyección de dependencias).</summary>
public static class Servicios
{
    public static Database Db { get; } = new(Path.Combine(FileSystem.AppDataDirectory, "msnsim.db3"));
    public static Simulador Sim { get; } = new(Db);
    public static Sincronizador Sync { get; } = new(Db);

    public static Usuario? Usuario { get; private set; }

    /// <summary>Email del contacto cuya conversación está en pantalla (para marcar leídos).</summary>
    public static string? ChatAbierto { get; set; }

    public static void IniciarSesion(Usuario u)
    {
        Usuario = u;
        Alertas.Iniciar();
        Sim.Iniciar();
        if (!string.IsNullOrEmpty(u.Token)) Sync.Iniciar();   // la cuenta de prueba no tiene servidor
        SegundoPlano.Iniciar();
    }

    public static void CerrarSesion()
    {
        Sim.Detener();
        Sync.Detener();
        SegundoPlano.Detener();
        Usuario = null;
        ChatAbierto = null;
    }
}
