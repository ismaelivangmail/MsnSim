using MsnSim.Data;

namespace MsnSim.Services;

public sealed record Invitacion(string Email, string Nombre);

/// <summary>
/// Punto único de eventos para la interfaz. Los emiten tanto el simulador (contactos demo)
/// como el sincronizador (contactos reales), así las pantallas no distinguen el origen.
/// </summary>
public static class Eventos
{
    public static event Action<Contacto, Estado>? PresenciaCambio;   // contacto actualizado, estado anterior
    public static event Action<Mensaje, Contacto>? MensajeRecibido;
    public static event Action<string, bool>? Escribiendo;           // email, sí/no
    public static event Action? ContactosCambiaron;                  // altas, bajas, perfiles, solicitudes
    public static event Action<Invitacion>? InvitacionNueva;
    public static event Action? SesionVencida;

    internal static void EmitirPresencia(Contacto c, Estado anterior) => PresenciaCambio?.Invoke(c, anterior);
    internal static void EmitirMensaje(Mensaje m, Contacto c) => MensajeRecibido?.Invoke(m, c);
    internal static void EmitirEscribiendo(string email, bool si) => Escribiendo?.Invoke(email, si);
    internal static void EmitirContactosCambiaron() => ContactosCambiaron?.Invoke();
    internal static void EmitirInvitacion(Invitacion i) => InvitacionNueva?.Invoke(i);
    internal static void EmitirSesionVencida() => SesionVencida?.Invoke();
}
