using MsnSim.Services;
using MsnSim.UI;

namespace MsnSim;

public class App : Application
{
    public App()
    {
        // Los colores están pensados para tema claro, como el Messenger de la época.
        UserAppTheme = AppTheme.Light;
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
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new(new LoginPage()) { Title = "MsnSim" };
}
