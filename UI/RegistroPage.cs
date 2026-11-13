using MsnSim.Services;

namespace MsnSim.UI;

/// <summary>Crear cuenta: correo → código recibido por correo → nombre y contraseña.</summary>
public class RegistroPage : ContentPage
{
    readonly Action<string> _alTerminar;
    readonly Entry _email = new() { Keyboard = Keyboard.Email, Placeholder = "nombre@ejemplo.com", FontSize = 15 };
    readonly Entry _codigo = new()
    {
        Keyboard = Keyboard.Numeric, MaxLength = 6, Placeholder = "000000", FontSize = 24,
        CharacterSpacing = 6, HorizontalTextAlignment = TextAlignment.Center
    };
    readonly Entry _nombre = new() { Placeholder = "Cómo te verán tus contactos", FontSize = 15, MaxLength = 60 };
    readonly Entry _pwd = new() { IsPassword = true, Placeholder = "Mínimo 8 caracteres", FontSize = 15 };
    readonly Entry _pwd2 = new() { IsPassword = true, Placeholder = "Repite la contraseña", FontSize = 15 };

    readonly Label _subtitulo = new() { FontSize = 14, TextColor = Tema.Gris, HorizontalTextAlignment = TextAlignment.Center };
    readonly Label _error = new() { TextColor = Color.FromArgb("#C62828"), FontSize = 13, IsVisible = false };
    readonly ActivityIndicator _cargando = new() { Color = Tema.Azul, IsVisible = false, HeightRequest = 32 };
    readonly VerticalStackLayout _paso1, _paso2, _paso3;
    readonly Button _reenviar;
    readonly List<Button> _botones = new();
    IDispatcherTimer? _cuentaAtras;
    int _segundos;
    string _emailEnVerificacion = "";
    string _token = "";

    public RegistroPage(Action<string> alTerminar)
    {
        _alTerminar = alTerminar;
        Background = Tema.Degradado("#FFFFFF", "#D9E9FA");

        _email.Completed += async (_, _) => await EnviarCodigoAsync();
        _codigo.TextChanged += async (_, e) => { if (e.NewTextValue?.Length == 6) await VerificarAsync(); };
        _pwd2.Completed += async (_, _) => await CrearCuentaAsync();

        _paso1 = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                Tema.Etiqueta("Dirección de correo electrónico:"), Tema.Caja(_email),
                Boton("Enviar código", EnviarCodigoAsync, principal: true)
            }
        };

        _reenviar = Boton("Reenviar código", ReenviarAsync, principal: false);
        _paso2 = new VerticalStackLayout
        {
            Spacing = 6, IsVisible = false,
            Children =
            {
                Tema.Etiqueta("Código de 6 dígitos:"), Tema.Caja(_codigo),
                Boton("Verificar", VerificarAsync, principal: true),
                _reenviar,
                Boton("Usar otro correo", () => { IrAPaso(1); return Task.CompletedTask; }, principal: false)
            }
        };

        _paso3 = new VerticalStackLayout
        {
            Spacing = 6, IsVisible = false,
            Children =
            {
                Tema.Etiqueta("Nombre para mostrar:"), Tema.Caja(_nombre),
                Tema.Etiqueta("Contraseña:"), Tema.Caja(_pwd),
                Tema.Etiqueta("Confirmar contraseña:"), Tema.Caja(_pwd2),
                Boton("Crear cuenta", CrearCuentaAsync, principal: true)
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(28, 40, 28, 24),
                Spacing = 18,
                MaximumWidthRequest = 460,
                Children =
                {
                    new Label { Text = "Crear una cuenta", FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = Tema.Azul, HorizontalTextAlignment = TextAlignment.Center },
                    _subtitulo,
                    _paso1, _paso2, _paso3,
                    _error, _cargando,
                    Boton("Cancelar", () => Navigation.PopModalAsync(), principal: false)
                }
            }
        };
        IrAPaso(1);
    }

    Button Boton(string texto, Func<Task> accion, bool principal)
    {
        var b = principal ? Tema.BotonPrincipal(texto) : Tema.BotonEnlace(texto);
        b.Clicked += async (_, _) => await accion();
        _botones.Add(b);
        return b;
    }

    void IrAPaso(int paso)
    {
        _paso1.IsVisible = paso == 1;
        _paso2.IsVisible = paso == 2;
        _paso3.IsVisible = paso == 3;
        _error.IsVisible = false;
        _subtitulo.Text = paso switch
        {
            1 => "Te enviaremos un código a tu correo para confirmar que es tuyo.",
            2 => $"Escribe el código que enviamos a {_emailEnVerificacion}. Vence en 10 minutos; si no lo ves, revisa la carpeta de spam.",
            _ => "Correo confirmado. Elige tu nombre y una contraseña."
        };
        if (paso != 2) _cuentaAtras?.Stop();
    }

    void Error(string? texto)
    {
        _error.Text = texto ?? "Ocurrió un error.";
        _error.IsVisible = true;
    }

    async Task<RespuestaApi> OcupadoAsync(Func<Task<RespuestaApi>> llamada)
    {
        _error.IsVisible = false;
        _cargando.IsVisible = _cargando.IsRunning = true;
        foreach (var b in _botones) b.IsEnabled = false;
        try { return await llamada(); }
        finally
        {
            _cargando.IsVisible = _cargando.IsRunning = false;
            foreach (var b in _botones) b.IsEnabled = true;
            ActualizarReenviar();
        }
    }

    // ---------- Paso 1 ----------

    async Task EnviarCodigoAsync()
    {
        var email = (_email.Text ?? "").Trim().ToLowerInvariant();
        var partes = email.Split('@');
        if (partes.Length != 2 || partes[0].Length == 0 || !partes[1].Contains('.'))
        {
            Error("Escribe una dirección de correo válida.");
            return;
        }
        var r = await OcupadoAsync(() => ApiCuentas.SolicitarCodigoAsync(email));
        if (!r.Ok) { Error(r.Error); return; }

        _emailEnVerificacion = email;
        _codigo.Text = "";
        IrAPaso(2);
        IniciarCuentaAtras();
        _codigo.Focus();
    }

    // ---------- Paso 2 ----------

    bool _verificando;

    async Task VerificarAsync()
    {
        var codigo = new string((_codigo.Text ?? "").Where(char.IsDigit).ToArray());
        if (codigo.Length != 6) { Error("El código tiene 6 dígitos."); return; }
        if (_verificando) return;
        _verificando = true;
        try
        {
            var r = await OcupadoAsync(() => ApiCuentas.VerificarCodigoAsync(_emailEnVerificacion, codigo));
            if (!r.Ok) { Error(r.Error); _codigo.Text = ""; return; }
            _token = r.Texto("token") ?? "";
            if (string.IsNullOrWhiteSpace(_nombre.Text)) _nombre.Text = _emailEnVerificacion.Split('@')[0];
            IrAPaso(3);
            _pwd.Focus();
        }
        finally { _verificando = false; }
    }

    async Task ReenviarAsync()
    {
        if (_segundos > 0) return;
        var r = await OcupadoAsync(() => ApiCuentas.SolicitarCodigoAsync(_emailEnVerificacion));
        if (!r.Ok) { Error(r.Error); return; }
        _codigo.Text = "";
        IniciarCuentaAtras();
        await DisplayAlert("Código reenviado", $"Enviamos un código nuevo a {_emailEnVerificacion}. El anterior ya no sirve.", "Aceptar");
    }

    void IniciarCuentaAtras()
    {
        _segundos = 60;
        _cuentaAtras ??= CrearTemporizador();
        _cuentaAtras.Start();
        ActualizarReenviar();
    }

    IDispatcherTimer CrearTemporizador()
    {
        var t = Dispatcher.CreateTimer();
        t.Interval = TimeSpan.FromSeconds(1);
        t.Tick += (_, _) =>
        {
            if (_segundos > 0) _segundos--;
            if (_segundos == 0) t.Stop();
            ActualizarReenviar();
        };
        return t;
    }

    void ActualizarReenviar()
    {
        _reenviar.Text = _segundos > 0 ? $"Reenviar código ({_segundos})" : "Reenviar código";
        _reenviar.IsEnabled = _segundos == 0 && !_cargando.IsVisible;
    }

    // ---------- Paso 3 ----------

    async Task CrearCuentaAsync()
    {
        var pwd = _pwd.Text ?? "";
        if (pwd.Length < 8) { Error("La contraseña debe tener al menos 8 caracteres."); return; }
        if (pwd != _pwd2.Text) { Error("Las contraseñas no coinciden."); return; }
        var nombre = (_nombre.Text ?? "").Trim();

        var r = await OcupadoAsync(() => ApiCuentas.CrearCuentaAsync(_emailEnVerificacion, _token, pwd, nombre));
        if (!r.Ok)
        {
            Error(r.Error);
            if (r.Error?.Contains("Vuelve a empezar") == true) IrAPaso(1);
            return;
        }

        await DisplayAlert("Cuenta creada", $"Ya puedes iniciar sesión con {_emailEnVerificacion}.", "Aceptar");
        _alTerminar(_emailEnVerificacion);
        await Navigation.PopModalAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _cuentaAtras?.Stop();
    }
}
