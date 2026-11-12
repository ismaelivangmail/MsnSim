using MsnSim.UI;

namespace MsnSim;

public class App : Application
{
    public App()
    {
        // Los colores están pensados para tema claro, como el Messenger de la época.
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new(new LoginPage()) { Title = "MsnSim" };
}
