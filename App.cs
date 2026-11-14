using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using MsnSim.Services;
using MsnSim.UI;

namespace MsnSim;

public class App : Microsoft.Maui.Controls.Application
{
    public App()
    {
        // Los colores están pensados para tema claro, como el Messenger de la época.
        UserAppTheme = AppTheme.Light;

        // El teclado encoge la pantalla en vez de empujarla hacia arriba:
        // el encabezado se queda fijo y la barra de escritura queda justo encima del teclado.
        this.On<global::Microsoft.Maui.Controls.PlatformConfiguration.Android>()
            .UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize);
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        Servicios.Sync.EnSegundoPlano = true;    // consulta cada 15 s en vez de cada 3 s
    }

    protected override void OnResume()
    {
        base.OnResume();
        Servicios.Sync.EnSegundoPlano = false;
        Servicios.Sync.Despertar();
        Notificaciones.LimpiarTodas();
        if (Servicios.Usuario is not null) SegundoPlano.Iniciar();   // por si Android lo detuvo
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new(new LoginPage()) { Title = "MsnSim" };
}
