using Microsoft.Maui.Controls.Shapes;
using MsnSim.Data;
using MsnSim.Services;

namespace MsnSim.UI;

public class ChatPage : ContentPage
{
    Contacto _c;
    readonly VerticalStackLayout _mensajes = new() { Spacing = 2, Padding = new Thickness(12, 10) };
    readonly ScrollView _scroll;
    readonly Entry _entrada;
    readonly Label _escribiendo;
    readonly Label _encNombre;
    readonly Label _encDetalle;
    readonly ContentView _encPunto = new() { VerticalOptions = LayoutOptions.Center };
    readonly ToolbarItem _itemBloquear;
    readonly Aviso _aviso = new();
    readonly Grid _cuerpo;
    readonly Border _panelEmojis;
    readonly Button _btnEmoji;
    VerticalStackLayout? _grupo;          // bloque de mensajes seguidos del mismo remitente
    string? _ultimoRemitente;
    bool _cargado;
    static readonly Dictionary<string, DateTime> UltimoZumbido = new();   // por contacto, aunque cierres el chat

    public ChatPage(Contacto c)
    {
        _c = c;
        Title = Tema.Emoticones(c.Nombre);
        BackgroundColor = Colors.White;

        _itemBloquear = new ToolbarItem { Order = ToolbarItemOrder.Secondary };
        _itemBloquear.Clicked += async (_, _) => await AlternarBloqueoAsync();
        var borrar = new ToolbarItem { Text = "Borrar historial", Order = ToolbarItemOrder.Secondary };
        borrar.Clicked += async (_, _) => await BorrarHistorialAsync();
        var eliminar = new ToolbarItem { Text = "Eliminar contacto", Order = ToolbarItemOrder.Secondary };
        eliminar.Clicked += async (_, _) => await EliminarContactoAsync();
        ToolbarItems.Add(_itemBloquear);
        ToolbarItems.Add(borrar);
        ToolbarItems.Add(eliminar);

        // Encabezado "Para:"
        _encNombre = new Label { FontSize = Tema.T(15), FontAttributes = FontAttributes.Bold, TextColor = Tema.Texto, LineBreakMode = LineBreakMode.TailTruncation };
        _encDetalle = new Label { FontSize = Tema.T(12), TextColor = Tema.Gris, LineBreakMode = LineBreakMode.TailTruncation };
        var encabezado = new Grid
        {
            Padding = new Thickness(12, 8),
            ColumnSpacing = 8,
            Background = Tema.Degradado("#EEF5FD", "#D3E5F8"),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)
            }
        };
        encabezado.Add(new Label { Text = "Para:", FontSize = Tema.T(13), TextColor = Tema.Gris, VerticalOptions = LayoutOptions.Center }, 0, 0);
        encabezado.Add(_encPunto, 1, 0);
        encabezado.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { _encNombre, _encDetalle } }, 2, 0);
        encabezado.Add(Tema.Avatar(c.Nombre, c.Email, 40, c.Avatar), 3, 0);

        _scroll = new ScrollView { Content = _mensajes };
        _escribiendo = new Label { FontSize = Tema.T(13), FontAttributes = FontAttributes.Italic, TextColor = Tema.Verde, Padding = new Thickness(14, 4), IsVisible = false };

        // Barra de escritura
        _entrada = new Entry { Placeholder = "Escribe un mensaje...", Keyboard = Keyboard.Chat, ReturnType = ReturnType.Send, FontSize = Tema.T(15) };
        _entrada.Completed += async (_, _) => await EnviarAsync();
        _entrada.Focused += async (_, _) =>
        {
            MostrarEmojis(false);
            await Task.Delay(300);        // espera a que el teclado termine de abrir
            await BajarAsync(false);
        };
        _entrada.TextChanged += (_, e) =>
        {
            if (_c.EsReal && !_c.Pendiente && !string.IsNullOrEmpty(e.NewTextValue))
                Servicios.Sync.AvisarEscribiendo(_c.Email);
        };
        var enviar = new Button
        {
            Text = "Enviar", BackgroundColor = Tema.Azul, TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(14, 0), HeightRequest = 42
        };
        enviar.Clicked += async (_, _) => await EnviarAsync();
        var marco = new Border
        {
            Content = _entrada,
            Padding = new Thickness(6, 0),
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Stroke = new SolidColorBrush(Tema.Borde),
            BackgroundColor = Colors.White
        };
        var barra = new Grid
        {
            Padding = new Thickness(8),
            ColumnSpacing = 6,
            BackgroundColor = Tema.AzulClaro,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)
            }
        };
        _btnEmoji = BotonIcono("🙂", AlternarEmojisAsync);
        barra.Add(_btnEmoji, 0, 0);
        barra.Add(BotonIcono("📳", EnviarZumbidoAsync), 1, 0);
        barra.Add(marco, 2, 0);
        barra.Add(enviar, 3, 0);

        _panelEmojis = CrearPanelEmojis();
        _cuerpo = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        _cuerpo.Add(encabezado, 0, 0);
        _cuerpo.Add(_scroll, 0, 1);
        _cuerpo.Add(_aviso, 0, 1);
        _cuerpo.Add(_escribiendo, 0, 2);
        _cuerpo.Add(barra, 0, 3);
        _cuerpo.Add(_panelEmojis, 0, 4);
        Content = _cuerpo;

        _aviso.Tocado += CambiarDeChatAsync;
        ActualizarEncabezado();
    }

    static Button BotonIcono(string texto, Func<Task> accion)
    {
        var b = new Button
        {
            Text = texto, FontSize = Tema.T(18), Padding = 0,
            BackgroundColor = Colors.White, TextColor = Tema.Texto,
            BorderColor = Tema.Borde, BorderWidth = 1, CornerRadius = 6,
            WidthRequest = 44, HeightRequest = 42
        };
        b.Clicked += async (_, _) => await accion();
        return b;
    }

    void ActualizarEncabezado()
    {
        _encPunto.Content = Tema.Punto(_c.Estado, 11);
        _encNombre.Text = Tema.Emoticones(_c.Nombre);
        var estado = _c.Bloqueado ? "Bloqueado"
            : _c.Pendiente ? "Pendiente: todavía no acepta tu invitación"
            : Tema.NombreEstado(_c.Estado);
        var personal = string.IsNullOrWhiteSpace(_c.MensajePersonal) ? "" : $"  –  {Tema.Emoticones(_c.MensajePersonal)}";
        // Como en WhatsApp: mientras escribe, el encabezado lo dice en lugar del estado.
        _encDetalle.Text = _contactoEscribiendo ? "escribiendo..." : estado + personal;
        _encDetalle.TextColor = _contactoEscribiendo ? Tema.Verde : Tema.Gris;
        _encDetalle.FontAttributes = _contactoEscribiendo ? FontAttributes.Italic : FontAttributes.None;
        _itemBloquear.Text = _c.Bloqueado ? "Desbloquear" : "Bloquear";
    }

    // ---------- Ciclo de vida ----------

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Servicios.ChatAbierto = _c.Email;
        Servicios.Sync.Despertar();   // trae el estado al momento
        Eventos.PresenciaCambio += OnPresencia;
        Eventos.MensajeRecibido += OnMensaje;
        Eventos.Escribiendo += OnEscribiendo;
        Eventos.ContactosCambiaron += OnContactosCambiaron;

        var u = Servicios.Usuario;
        if (u is null) return;
        await Servicios.Db.MarcarLeidosAsync(u.Email, _c.Email);

        if (_cargado) return;
        _cargado = true;
        Sistema("Nunca des tu contraseña ni datos personales en una conversación.");
        foreach (var m in await Servicios.Db.HistorialAsync(u.Email, _c.Email))
            Agregar(m);
        await BajarAsync(false);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Servicios.ChatAbierto == _c.Email) Servicios.ChatAbierto = null;
        Eventos.PresenciaCambio -= OnPresencia;
        Eventos.MensajeRecibido -= OnMensaje;
        Eventos.Escribiendo -= OnEscribiendo;
        Eventos.ContactosCambiaron -= OnContactosCambiaron;
    }

    // ---------- Eventos del simulador ----------

    void OnPresencia(Contacto c, Estado anterior)
    {
        if (c.Email != _c.Email) return;
        _c = c;
        ActualizarEncabezado();
        bool antes = Simulador.EstaEnLinea(anterior), ahora = Simulador.EstaEnLinea(c);
        var nombre = Tema.Emoticones(c.Nombre);
        if (antes && !ahora) Sistema($"{nombre} ha cerrado sesión.");
        else if (!antes && ahora) Sistema($"{nombre} ha iniciado sesión.");
        else if (anterior != c.Estado) Sistema($"{nombre} ha cambiado su estado a {Tema.NombreEstado(c.Estado)}.");
        _ = BajarAsync();
    }

    async void OnContactosCambiaron()
    {
        var u = Servicios.Usuario;
        if (u is null) return;
        var actual = await Servicios.Db.ContactoPorEmailAsync(u.Email, _c.Email);
        if (actual is null) return;   // lo quitaron; la pantalla sigue mostrando el historial
        bool aceptoAhora = _c.Pendiente && !actual.Pendiente && !actual.Bloqueado;
        _c = actual;
        Title = Tema.Emoticones(_c.Nombre);
        ActualizarEncabezado();
        if (aceptoAhora)
        {
            Sistema($"{Tema.Emoticones(_c.Nombre)} aceptó tu invitación. Ya pueden escribirse.");
            await BajarAsync();
        }
    }

    async void OnMensaje(Mensaje m, Contacto c)
    {
        if (m.Contacto != _c.Email)
        {
            Tema.Vibrar(150);
            var nombre = Tema.Emoticones(c.Nombre);
            _aviso.Mostrar(m.EsZumbido
                ? $"{nombre}\nte ha enviado un zumbido."
                : $"{nombre} dice:\n{Tema.Emoticones(m.Texto)}", c.Email,
                Tema.Avatar(c.Nombre, c.Email, Aviso.LadoIcono, c.Avatar));
            return;
        }
        _escribiendo.IsVisible = false;
        Agregar(m);
        await BajarAsync();
        if (m.EsZumbido) await SacudirAsync();
    }

    bool _contactoEscribiendo;

    void OnEscribiendo(string email, bool escribiendo)
    {
        if (!string.Equals(email, _c.Email, StringComparison.OrdinalIgnoreCase)) return;
        _contactoEscribiendo = escribiendo;
        _escribiendo.Text = $"{Tema.Emoticones(_c.Nombre)} está escribiendo un mensaje...";
        _escribiendo.IsVisible = escribiendo;
        ActualizarEncabezado();
        if (escribiendo) _ = BajarAsync();
    }

    // ---------- Pintado ----------

    void Agregar(Mensaje m)
    {
        if (m.EsZumbido)
        {
            Sistema(m.Enviado ? "Has enviado un zumbido." : $"{Tema.Emoticones(_c.Nombre)} te ha enviado un zumbido.");
            return;
        }

        var clave = m.Enviado ? "yo" : _c.Email;
        if (_grupo is null || _ultimoRemitente != clave)
            NuevoGrupo(m);
        _grupo!.Children.Add(new Label
        {
            Text = Tema.Emoticones(m.Texto),
            FontSize = Tema.T(15),
            TextColor = m.Enviado ? Tema.Texto : Tema.ColorDe(_c.Email)
        });
    }

    /// <summary>
    /// Cabecera "X dice (hora):" y, para los mensajes recibidos, el avatar de quien escribe
    /// a la izquierda de todo el bloque.
    /// </summary>
    void NuevoGrupo(Mensaje m)
    {
        var u = Servicios.Usuario;
        var remitente = m.Enviado ? u?.Nombre ?? "Yo" : _c.Nombre;
        var hora = m.Fecha.Date == DateTime.Today ? m.Fecha.ToString("HH:mm") : m.Fecha.ToString("dd/MM HH:mm");

        _grupo = new VerticalStackLayout { Spacing = 2 };
        _grupo.Children.Add(new Label
        {
            Text = $"{Tema.Emoticones(remitente)} dice ({hora}):",
            FontSize = Tema.T(12),
            TextColor = Tema.Gris
        });

        var fila = new Grid
        {
            ColumnSpacing = 10,
            Margin = new Thickness(0, 10, 0, 0),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }
        };
        View avatar = m.Enviado
            ? new BoxView { WidthRequest = 36, Color = Colors.Transparent }   // mantiene la alineación
            : Tema.Avatar(_c.Nombre, _c.Email, 36, _c.Avatar);
        avatar.VerticalOptions = LayoutOptions.Start;
        fila.Add(avatar, 0, 0);
        fila.Add(_grupo, 1, 0);

        _mensajes.Children.Add(fila);
        _ultimoRemitente = m.Enviado ? "yo" : _c.Email;
    }

    void Sistema(string texto)
    {
        _mensajes.Children.Add(new Label
        {
            Text = texto,
            FontSize = Tema.T(12),
            FontAttributes = FontAttributes.Italic,
            TextColor = Tema.Gris,
            Margin = new Thickness(0, 8, 0, 0)
        });
        _grupo = null;
        _ultimoRemitente = null;
    }

    async Task BajarAsync(bool animar = true)
    {
        await Task.Delay(60);
        await _scroll.ScrollToAsync(_mensajes, ScrollToPosition.End, animar);
    }

    async Task SacudirAsync()
    {
        Tema.Vibrar(500);
        double[] xs = { -16, 14, -12, 10, -8, 6, -3, 0 };
        double[] ys = { 6, -6, 4, -4, 3, -2, 1, 0 };
        for (int i = 0; i < xs.Length; i++)
            await _cuerpo.TranslateTo(xs[i], ys[i], 40);
    }

    // ---------- Acciones ----------

    bool PuedeEntregar(string? textoPendiente)
    {
        var nombre = Tema.Emoticones(_c.Nombre);
        if (_c.Bloqueado)
        {
            Sistema($"Bloqueaste a {nombre}. Desbloquéalo para enviarle mensajes.");
            return false;
        }
        if (_c.EsReal)
        {
            if (_c.Pendiente)
            {
                Sistema($"{nombre} todavía no acepta tu invitación. Podrán escribirse cuando te agregue.");
                return false;
            }
            return true;   // si está desconectado, el servidor guarda el mensaje hasta que entre
        }
        if (!Simulador.EstaEnLinea(_c))
        {
            Sistema(textoPendiente is null
                ? $"{nombre} aparece como Desconectado y no puede recibir zumbidos."
                : $"No se pudo entregar el siguiente mensaje porque {nombre} aparece como Desconectado:\n{textoPendiente}");
            return false;
        }
        return true;
    }

    /// <summary>Envía al servidor (contactos reales). Devuelve false si no se pudo.</summary>
    async Task<bool> EnviarRealAsync(string texto, bool zumbido)
    {
        var r = await Servicios.Sync.EnviarAsync(_c.Email, texto, zumbido);
        if (!r.Ok)
        {
            Sistema(zumbido ? $"No se pudo enviar el zumbido: {r.Error}" : $"No se pudo enviar el mensaje: {r.Error}\n{texto}");
            await BajarAsync();
            return false;
        }
        if (!r.Booleano("en_linea") && !zumbido && !_avisoDesconectado)
        {
            _avisoDesconectado = true;
            Sistema($"{Tema.Emoticones(_c.Nombre)} está desconectado. Recibirá tus mensajes cuando inicie sesión.");
        }
        return true;
    }

    bool _avisoDesconectado;

    async Task EnviarAsync()
    {
        var u = Servicios.Usuario;
        var texto = _entrada.Text?.Trim();
        if (u is null || string.IsNullOrEmpty(texto)) return;

        if (!PuedeEntregar(texto))
        {
            await BajarAsync();
            return;
        }

        _entrada.Text = "";
        if (_c.EsReal && !await EnviarRealAsync(texto, false))
        {
            _entrada.Text = texto;   // se queda para reintentar
            return;
        }

        var m = new Mensaje
        {
            Propietario = u.Email, Contacto = _c.Email, Enviado = true,
            Texto = texto, Fecha = DateTime.Now, Leido = true
        };
        await Servicios.Db.GuardarMensajeAsync(m);
        Agregar(m);
        await BajarAsync();
        _entrada.Focus();
        if (!_c.EsReal) _ = Servicios.Sim.ResponderAsync(_c, texto);
    }

    async Task EnviarZumbidoAsync()
    {
        var u = Servicios.Usuario;
        if (u is null) return;
        int espera = Servicios.Sync.SegundosZumbido;
        var faltan = UltimoZumbido.TryGetValue(_c.Email, out var ultimo) ? espera - (DateTime.Now - ultimo).TotalSeconds : 0;
        if (faltan > 0)
        {
            Sistema($"No puedes enviar zumbidos con tanta frecuencia. Espera {Math.Ceiling(faltan)} segundos.");
            await BajarAsync();
            return;
        }
        if (!PuedeEntregar(null))
        {
            await BajarAsync();
            return;
        }

        UltimoZumbido[_c.Email] = DateTime.Now;
        if (_c.EsReal && !await EnviarRealAsync("", true)) return;
        Sonidos.Zumbido();
        var m = new Mensaje
        {
            Propietario = u.Email, Contacto = _c.Email, Enviado = true,
            EsZumbido = true, Fecha = DateTime.Now, Leido = true
        };
        await Servicios.Db.GuardarMensajeAsync(m);
        Agregar(m);
        await BajarAsync();
        await SacudirAsync();
        if (!_c.EsReal) _ = Servicios.Sim.ZumbidoEnviadoAsync(_c);
    }

    // ---------- Panel de emojis ----------

    Border CrearPanelEmojis()
    {
        const int columnas = 7;
        var grid = new Grid { RowSpacing = 4, ColumnSpacing = 4 };
        for (int i = 0; i < columnas; i++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var celdas = Tema.EmojisPanel().Append("⌫").ToArray();
        for (int i = 0; i < celdas.Length; i++)
        {
            if (i % columnas == 0) grid.RowDefinitions.Add(new RowDefinition(new GridLength(46)));
            var texto = celdas[i];
            var celda = new Label
            {
                Text = texto,
                FontSize = texto == "⌫" ? 22 : 26,
                TextColor = Tema.Texto,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
            Tema.AlTocar(celda, () =>
            {
                if (texto == "⌫") BorrarUltimo(); else InsertarEmoji(texto);
                return Task.CompletedTask;
            });
            grid.Add(celda, i % columnas, i / columnas);
        }

        return new Border
        {
            Content = grid,
            Padding = new Thickness(8, 6, 8, 10),
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#F3F7FC"),
            IsVisible = false
        };
    }

    void MostrarEmojis(bool mostrar)
    {
        _panelEmojis.IsVisible = mostrar;
        _btnEmoji.Text = mostrar ? "⌨️" : "🙂";
    }

    async Task AlternarEmojisAsync()
    {
        if (_panelEmojis.IsVisible)
        {
            MostrarEmojis(false);
            _entrada.Focus();   // vuelve el teclado, como en WhatsApp
            return;
        }
        try { await _entrada.HideSoftInputAsync(CancellationToken.None); } catch { }
        _entrada.Unfocus();
        MostrarEmojis(true);
        await BajarAsync(false);
    }

    void InsertarEmoji(string emoji)
    {
        var t = _entrada.Text ?? "";
        int pos = Math.Clamp(_entrada.CursorPosition, 0, t.Length);
        _entrada.Text = t.Insert(pos, emoji);
        _entrada.CursorPosition = pos + emoji.Length;
    }

    void BorrarUltimo()
    {
        var t = _entrada.Text ?? "";
        if (t.Length == 0) return;
        var info = new System.Globalization.StringInfo(t);   // un emoji puede ocupar varios caracteres
        _entrada.Text = info.SubstringByTextElements(0, info.LengthInTextElements - 1);
        _entrada.CursorPosition = _entrada.Text.Length;
    }

    async Task AlternarBloqueoAsync()
    {
        if (_c.EsReal)
        {
            var r = await Servicios.Sync.CambiarBloqueoAsync(_c.Email, !_c.Bloqueado);
            if (!r.Ok)
            {
                await DisplayAlert("Bloquear", r.Error, "Aceptar");
                return;
            }
            _c.Relacion = !_c.Bloqueado ? "bloqueado" : "aceptado";   // la sincronización lo confirma
            if (!_c.Bloqueado) _c.Estado = Estado.Desconectado;
        }
        _c.Bloqueado = !_c.Bloqueado;
        await Servicios.Db.GuardarContactoAsync(_c);
        ActualizarEncabezado();
        var nombre = Tema.Emoticones(_c.Nombre);
        Sistema(_c.Bloqueado
            ? $"Bloqueaste a {nombre}. Ya no podrá verte ni enviarte mensajes."
            : $"Desbloqueaste a {nombre}.");
        await BajarAsync();
    }

    async Task BorrarHistorialAsync()
    {
        var u = Servicios.Usuario;
        if (u is null) return;
        if (!await DisplayAlert("Borrar historial", $"¿Borrar toda la conversación con {_c.Nombre}?", "Borrar", "Cancelar"))
            return;
        await Servicios.Db.BorrarHistorialAsync(u.Email, _c.Email);
        _mensajes.Children.Clear();
        _grupo = null;
        _ultimoRemitente = null;
        Sistema("Se borró el historial de esta conversación.");
    }

    async Task EliminarContactoAsync()
    {
        if (!await DisplayAlert("Eliminar contacto", $"¿Eliminar a {_c.Nombre} ({_c.Email}) y su historial?", "Eliminar", "Cancelar"))
            return;
        if (_c.EsReal)
        {
            var r = await Servicios.Sync.EliminarAsync(_c.Email);
            if (!r.Ok)
            {
                await DisplayAlert("Eliminar contacto", r.Error, "Aceptar");
                return;
            }
        }
        await Servicios.Db.EliminarContactoAsync(_c);
        await Navigation.PopAsync();
    }

    async Task CambiarDeChatAsync(string email)
    {
        var u = Servicios.Usuario;
        if (u is null) return;
        var otro = (await Servicios.Db.ContactosAsync(u.Email)).FirstOrDefault(x => x.Email == email);
        if (otro is null) return;
        var nav = Navigation;
        await nav.PopAsync(false);
        await nav.PushAsync(new ChatPage(otro));
    }
}
