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
    string? _ultimoRemitente;
    bool _cargado;
    DateTime _ultimoZumbido = DateTime.MinValue;

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
        _encNombre = new Label { FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Tema.Texto, LineBreakMode = LineBreakMode.TailTruncation };
        _encDetalle = new Label { FontSize = 12, TextColor = Tema.Gris, LineBreakMode = LineBreakMode.TailTruncation };
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
        encabezado.Add(new Label { Text = "Para:", FontSize = 13, TextColor = Tema.Gris, VerticalOptions = LayoutOptions.Center }, 0, 0);
        encabezado.Add(_encPunto, 1, 0);
        encabezado.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { _encNombre, _encDetalle } }, 2, 0);
        encabezado.Add(Tema.Avatar(c.Nombre, c.Email, 40), 3, 0);

        _scroll = new ScrollView { Content = _mensajes };
        _escribiendo = new Label { FontSize = 12, FontAttributes = FontAttributes.Italic, TextColor = Tema.Gris, Padding = new Thickness(14, 2), IsVisible = false };

        // Barra de escritura
        _entrada = new Entry { Placeholder = "Escribe un mensaje...", Keyboard = Keyboard.Chat, ReturnType = ReturnType.Send, FontSize = 15 };
        _entrada.Completed += async (_, _) => await EnviarAsync();
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
        barra.Add(BotonIcono("🙂", ElegirEmoticonAsync), 0, 0);
        barra.Add(BotonIcono("📳", EnviarZumbidoAsync), 1, 0);
        barra.Add(marco, 2, 0);
        barra.Add(enviar, 3, 0);

        _cuerpo = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)
            }
        };
        _cuerpo.Add(encabezado, 0, 0);
        _cuerpo.Add(_scroll, 0, 1);
        _cuerpo.Add(_aviso, 0, 1);
        _cuerpo.Add(_escribiendo, 0, 2);
        _cuerpo.Add(barra, 0, 3);
        Content = _cuerpo;

        _aviso.Tocado += CambiarDeChatAsync;
        ActualizarEncabezado();
    }

    static Button BotonIcono(string texto, Func<Task> accion)
    {
        var b = new Button
        {
            Text = texto, FontSize = 18, Padding = 0,
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
        var estado = _c.Bloqueado ? "Bloqueado" : Tema.NombreEstado(_c.Estado);
        var personal = string.IsNullOrWhiteSpace(_c.MensajePersonal) ? "" : $"  –  {Tema.Emoticones(_c.MensajePersonal)}";
        _encDetalle.Text = $"<{_c.Email}> ({estado}){personal}";
        _itemBloquear.Text = _c.Bloqueado ? "Desbloquear" : "Bloquear";
    }

    // ---------- Ciclo de vida ----------

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Servicios.ChatAbierto = _c.Email;
        Servicios.Sim.PresenciaCambio += OnPresencia;
        Servicios.Sim.MensajeRecibido += OnMensaje;
        Servicios.Sim.Escribiendo += OnEscribiendo;

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
        Servicios.Sim.PresenciaCambio -= OnPresencia;
        Servicios.Sim.MensajeRecibido -= OnMensaje;
        Servicios.Sim.Escribiendo -= OnEscribiendo;
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

    async void OnMensaje(Mensaje m, Contacto c)
    {
        if (m.Contacto != _c.Email)
        {
            Tema.Vibrar(150);
            var nombre = Tema.Emoticones(c.Nombre);
            _aviso.Mostrar(m.EsZumbido
                ? $"{nombre}\nte ha enviado un zumbido."
                : $"{nombre} dice:\n{Tema.Emoticones(m.Texto)}", c.Email);
            return;
        }
        _escribiendo.IsVisible = false;
        Agregar(m);
        await BajarAsync();
        if (m.EsZumbido) await SacudirAsync();
    }

    void OnEscribiendo(string email, bool escribiendo)
    {
        if (email != _c.Email) return;
        _escribiendo.Text = $"{Tema.Emoticones(_c.Nombre)} está escribiendo un mensaje...";
        _escribiendo.IsVisible = escribiendo;
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
        if (_ultimoRemitente != clave)
        {
            var remitente = m.Enviado ? Servicios.Usuario?.Nombre ?? "Yo" : _c.Nombre;
            var hora = m.Fecha.Date == DateTime.Today ? m.Fecha.ToString("HH:mm") : m.Fecha.ToString("dd/MM HH:mm");
            _mensajes.Children.Add(new Label
            {
                Text = $"{Tema.Emoticones(remitente)} dice ({hora}):",
                FontSize = 12,
                TextColor = Tema.Gris,
                Margin = new Thickness(0, 8, 0, 0)
            });
            _ultimoRemitente = clave;
        }
        _mensajes.Children.Add(new Label
        {
            Text = Tema.Emoticones(m.Texto),
            FontSize = 15,
            TextColor = m.Enviado ? Tema.Texto : Tema.ColorDe(_c.Email),
            Margin = new Thickness(12, 0, 0, 0)
        });
    }

    void Sistema(string texto)
    {
        _mensajes.Children.Add(new Label
        {
            Text = texto,
            FontSize = 12,
            FontAttributes = FontAttributes.Italic,
            TextColor = Tema.Gris,
            Margin = new Thickness(0, 8, 0, 0)
        });
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
        if (!Simulador.EstaEnLinea(_c))
        {
            Sistema(textoPendiente is null
                ? $"{nombre} aparece como Desconectado y no puede recibir zumbidos."
                : $"No se pudo entregar el siguiente mensaje porque {nombre} aparece como Desconectado:\n{textoPendiente}");
            return false;
        }
        return true;
    }

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
        var m = new Mensaje
        {
            Propietario = u.Email, Contacto = _c.Email, Enviado = true,
            Texto = texto, Fecha = DateTime.Now, Leido = true
        };
        await Servicios.Db.GuardarMensajeAsync(m);
        Agregar(m);
        await BajarAsync();
        _entrada.Focus();
        _ = Servicios.Sim.ResponderAsync(_c, texto);
    }

    async Task EnviarZumbidoAsync()
    {
        var u = Servicios.Usuario;
        if (u is null) return;
        if ((DateTime.Now - _ultimoZumbido).TotalSeconds < 8)
        {
            Sistema("No puedes enviar zumbidos con tanta frecuencia.");
            await BajarAsync();
            return;
        }
        if (!PuedeEntregar(null))
        {
            await BajarAsync();
            return;
        }

        _ultimoZumbido = DateTime.Now;
        var m = new Mensaje
        {
            Propietario = u.Email, Contacto = _c.Email, Enviado = true,
            EsZumbido = true, Fecha = DateTime.Now, Leido = true
        };
        await Servicios.Db.GuardarMensajeAsync(m);
        Agregar(m);
        await BajarAsync();
        await SacudirAsync();
        _ = Servicios.Sim.ZumbidoEnviadoAsync(_c);
    }

    async Task ElegirEmoticonAsync()
    {
        var r = await DisplayActionSheet("Emoticonos", "Cancelar", null, Tema.MenuEmoticones());
        var codigo = Tema.CodigoDeOpcion(r);
        if (codigo is null) return;
        _entrada.Text = (_entrada.Text ?? "") + codigo;
        _entrada.Focus();
    }

    async Task AlternarBloqueoAsync()
    {
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
        _ultimoRemitente = null;
        Sistema("Se borró el historial de esta conversación.");
    }

    async Task EliminarContactoAsync()
    {
        if (!await DisplayAlert("Eliminar contacto", $"¿Eliminar a {_c.Nombre} ({_c.Email}) y su historial?", "Eliminar", "Cancelar"))
            return;
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
