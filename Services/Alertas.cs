using Microsoft.Maui.Storage;
using MsnSim.Data;

namespace MsnSim.Services;

/// <summary>
/// Decide cómo avisar: con la app abierta, sonido; en segundo plano, notificación de Android.
/// Se suscribe una sola vez a los eventos (de contactos demo y reales por igual).
/// </summary>
public static class Alertas
{
    static bool _suscrito;

    public static bool SonidosActivados
    {
        get => Preferences.Default.Get("sonidos", true);
        set => Preferences.Default.Set("sonidos", value);
    }

    static bool EnSegundoPlano => Servicios.Sync.EnSegundoPlano;

    public static void Iniciar()
    {
        Sonidos.Iniciar();
        if (_suscrito) return;
        _suscrito = true;
        Eventos.MensajeRecibido += OnMensaje;
        Eventos.PresenciaCambio += OnPresencia;
    }

    static void OnMensaje(Mensaje m, Contacto c)
    {
        if (Servicios.Usuario is null) return;
        if (EnSegundoPlano)
        {
            Notificaciones.Mostrar(c, m);
            return;
        }
        if (m.EsZumbido) Sonidos.Zumbido();
        else Sonidos.Mensaje();
    }

    static void OnPresencia(Contacto c, Estado anterior)
    {
        if (Servicios.Usuario is null || EnSegundoPlano) return;
        if (!Simulador.EstaEnLinea(anterior) && Simulador.EstaEnLinea(c))
            Sonidos.Conectado();
    }
}
