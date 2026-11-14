using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using MsnSim.Data;
using MsnSim.Services;

namespace MsnSim.UI;

public class LoginPage : ContentPage
{
    const string ClaveRecordado = "email_recordado";
    public const string ClaveAutomatico = "inicio_automatico";   // correo con el que se entra solo

    readonly bool _intentarAutomatico;
    readonly CheckBox _automatico;

    readonly Entry _email;
    readonly Entry _pwd;
    readonly Picker _estado;
    readonly CheckBox _recordar;
    readonly Label _error;
    readonly VerticalStackLayout _formulario;
    readonly VerticalStackLayout _cargando;

    public LoginPage(bool intentarAutomatico = true)
    {
        _intentarAutomatico = intentarAutomatico;
        Background = Tema.Degradado("#FFFFFF", "#D9E9FA");

        var recordado = Preferences.Default.Get(ClaveRecordado, "");
        _email = new Entry
        {
            Text = recordado.Length > 0 ? recordado : Database.UsuarioDemo,
            Keyboard = Keyboard.Email,
            Placeholder = "nombre@msn.com",
            FontSize = Tema.T(15)
        };
        _pwd = new Entry { IsPassword = true, Placeholder = "Contraseña", FontSize = Tema.T(15), ReturnType = ReturnType.Go };
        _pwd.Completed += async (_, _) => await IniciarSesionAsync();

        _estado = new Picker { Title = "Estado", FontSize = Tema.T(15) };
        foreach (var e in Tema.Seleccionables) _estado.Items.Add(Tema.NombreEstado(e));
        _estado.SelectedIndex = 0;

        _recordar = new CheckBox { IsChecked = recordado.Length > 0, Color = Tema.Azul };
        _automatico = new CheckBox { IsChecked = Preferences.Default.Get(ClaveAutomatico, "").Length > 0, Color = Tema.Azul };
        // Entrar solo implica recordar la dirección.
        _automatico.CheckedChanged += (_, e) => { if (e.Value) _recordar.IsChecked = true; };

        var entrar = new Button
        {
            Text = "Iniciar sesión",
            BackgroundColor = Tema.Azul,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 6,
            HeightRequest = 46,
            Margin = new Thickness(0, 8, 0, 0)
        };
        entrar.Clicked += async (_, _) => await IniciarSesionAsync();

        _error = new Label { TextColor = Color.FromArgb("#C62828"), FontSize = Tema.T(13), IsVisible = false };

        _formulario = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                Etiqueta("Dirección de correo electrónico:"), Caja(_email),
                Etiqueta("Contraseña:"), Caja(_pwd),
                Etiqueta("Iniciar sesión como:"), Caja(_estado),
                new HorizontalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        _recordar,
                        new Label { Text = "Recordar mi dirección", FontSize = Tema.T(14), TextColor = Tema.Texto, VerticalOptions = LayoutOptions.Center }
                    }
                },
                new HorizontalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        _automatico,
                        new Label { Text = "Iniciar sesión automáticamente", FontSize = Tema.T(14), TextColor = Tema.Texto, VerticalOptions = LayoutOptions.Center }
                    }
                },
                _error,
                entrar,
                CrearCuentaBoton(),
                new Label
                {
                    Text = "Cuenta de prueba: ismaelivan@msn.com, contraseña 123",
                    FontSize = Tema.T(12), TextColor = Tema.Gris,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 12, 0, 0)
                }
            }
        };

        _cargando = new VerticalStackLayout
        {
            Spacing = 14,
            IsVisible = false,
            Margin = new Thickness(0, 30),
            Children =
            {
                new ActivityIndicator { IsRunning = true, Color = Tema.Azul, HeightRequest = 40 },
                new Label { Text = "Iniciando sesión...", FontSize = Tema.T(15), TextColor = Tema.Gris, HorizontalTextAlignment = TextAlignment.Center }
            }
        };

        var cabecera = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Logo(),
                new Label { Text = "MsnSim", FontSize = Tema.T(30), FontAttributes = FontAttributes.Bold, TextColor = Tema.Azul, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = "Mensajería instantánea · Modo en Desarrollo...", FontSize = Tema.T(13), TextColor = Tema.Gris, HorizontalTextAlignment = TextAlignment.Center }
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(28, 48, 28, 24),
                Spacing = 22,
                MaximumWidthRequest = 460,
                Children = { cabecera, _formulario, _cargando }
            }
        };
    }

    Button CrearCuentaBoton()
    {
        var b = Tema.BotonEnlace("¿No tienes cuenta? Crear una");
        b.Clicked += async (_, _) =>
            await Navigation.PushModalAsync(new RegistroPage(email =>
            {
                _email.Text = email;
                _pwd.Text = "";
                _pwd.Focus();
            }));
        return b;
    }

    static Label Etiqueta(string texto) =>
        new() { Text = texto, FontSize = Tema.T(13), TextColor = Tema.Texto, Margin = new Thickness(0, 6, 0, 0) };

    static Border Caja(View contenido) => new()
    {
        Content = contenido,
        Padding = new Thickness(8, 0),
        StrokeShape = new RoundRectangle { CornerRadius = 6 },
        Stroke = new SolidColorBrush(Tema.Borde),
        BackgroundColor = Colors.White
    };

    static View Logo()
    {
        var atras = new Border
        {
            WidthRequest = 58, HeightRequest = 44, StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#8DBCEB"),
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            TranslationX = 16, TranslationY = -10
        };
        var frente = new Border
        {
            WidthRequest = 58, HeightRequest = 44, StrokeThickness = 0,
            BackgroundColor = Tema.Azul,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            TranslationX = -12, TranslationY = 8,
            Content = new Label
            {
                Text = "• • •", TextColor = Colors.White, FontSize = Tema.T(16), FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
            }
        };
        return new Grid { WidthRequest = 110, HeightRequest = 86, HorizontalOptions = LayoutOptions.Center, Children = { atras, frente } };
    }

    bool _automaticoIntentado;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_intentarAutomatico || _automaticoIntentado) return;
        _automaticoIntentado = true;

        var email = Preferences.Default.Get(ClaveAutomatico, "");
        if (email.Length == 0)
        {
            await Actualizaciones.RevisarAlIniciarAsync(this);   // con inicio automático se revisa ya adentro
            return;
        }

        _formulario.IsVisible = false;
        _cargando.IsVisible = true;
        var u = await Servicios.Db.UsuarioAsync(email);

        // La sesión del servidor es la credencial; si vence, el sincronizador avisa y vuelve aquí.
        if (u is not null && (email == Database.UsuarioDemo || !string.IsNullOrEmpty(u.Token)))
        {
            u.UltimoAcceso = DateTime.Now;
            await Servicios.Db.GuardarUsuarioAsync(u);
            Entrar(u);
            return;
        }

        Preferences.Default.Remove(ClaveAutomatico);
        _cargando.IsVisible = false;
        _formulario.IsVisible = true;
    }

    void Error(string texto)
    {
        _error.Text = texto;
        _error.IsVisible = true;
    }

    async Task IniciarSesionAsync()
    {
        _error.IsVisible = false;
        var email = (_email.Text ?? "").Trim().ToLowerInvariant();
        var pwd = _pwd.Text ?? "";
        if (email.Length == 0 || pwd.Length == 0)
        {
            Error("Escribe tu dirección de correo y tu contraseña.");
            return;
        }

        _formulario.IsVisible = false;
        _cargando.IsVisible = true;
        await Task.Delay(700); // la espera clásica del login

        Usuario? u = null;
        string? error = null;

        if (email == Database.UsuarioDemo)
        {
            // Cuenta de prueba: sólo existe en el teléfono.
            u = await Servicios.Db.ValidarAsync(email, pwd);
        }
        else
        {
            // 1) El servidor manda: valida la contraseña y entrega una sesión nueva.
            var r = await ApiCuentas.IniciarSesionAsync(email, pwd);
            if (r.Ok)
            {
                u = await Servicios.Db.GuardarCuentaRemotaAsync(
                    email,
                    r.TextoUsuario("nombre") ?? email.Split('@')[0],
                    r.TextoUsuario("mensaje_personal") ?? "",
                    pwd,
                    r.Texto("sesion"),
                    r.TextoUsuario("avatar"));

                if (u.AvatarRemoto is { } remoto && !Avatares.Existe(u.Avatar))
                {
                    u.Avatar = await Avatares.DescargarAsync(remoto, u.Avatar);
                    await Servicios.Db.GuardarUsuarioAsync(u);
                }
            }
            else if (r.SinConexion)
            {
                // 2) Sin internet: se permite entrar con la copia guardada en el teléfono.
                // Si quedó una sesión guardada se conserva, para sincronizar cuando vuelva la conexión.
                u = await Servicios.Db.ValidarAsync(email, pwd);
                if (u is null) error = r.Error;
            }
            else
            {
                error = r.Error;
            }
        }

        if (u is null)
        {
            _cargando.IsVisible = false;
            _formulario.IsVisible = true;
            _pwd.Text = "";
            Error(error ?? "No se pudo iniciar sesión: la dirección de correo o la contraseña no son correctas.");
            return;
        }

        u.Estado = Tema.Seleccionables[Math.Max(0, _estado.SelectedIndex)];
        u.UltimoAcceso = DateTime.Now;
        await Servicios.Db.GuardarUsuarioAsync(u);

        if (_recordar.IsChecked) Preferences.Default.Set(ClaveRecordado, email);
        else Preferences.Default.Remove(ClaveRecordado);
        // Sólo se puede entrar solo si hay con qué: la cuenta de prueba o una sesión del servidor.
        if (_automatico.IsChecked && (email == Database.UsuarioDemo || !string.IsNullOrEmpty(u.Token)))
            Preferences.Default.Set(ClaveAutomatico, email);
        else
            Preferences.Default.Remove(ClaveAutomatico);

        Entrar(u);
    }

    void Entrar(Usuario u)
    {
        Servicios.IniciarSesion(u);
        Application.Current!.Windows[0].Page = ContactsPage.CrearRaiz();
    }
}
