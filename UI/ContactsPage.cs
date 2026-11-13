using Microsoft.Maui.Controls.Shapes;
using MsnSim.Data;
using MsnSim.Services;

namespace MsnSim.UI;

public class ContactsPage : ContentPage
{
    readonly ContentView _avatar = new();
    readonly Ellipse _punto = Tema.Punto(Estado.Disponible, 11);
    readonly Label _nombre = new() { TextColor = Colors.White, FontSize = 18, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation };
    readonly Label _estado = new() { TextColor = Colors.White, FontSize = 13 };
    readonly Label _personal = new() { TextColor = Color.FromArgb("#EAF3FF"), FontSize = 13, FontAttributes = FontAttributes.Italic, LineBreakMode = LineBreakMode.TailTruncation };
    readonly Entry _buscar = new() { Placeholder = "Buscar contactos...", FontSize = 14, ClearButtonVisibility = ClearButtonVisibility.WhileEditing };
    readonly VerticalStackLayout _lista = new() { Padding = new Thickness(0, 0, 0, 90) };
    readonly Aviso _aviso = new();
    readonly HashSet<string> _colapsados = new();
    bool _navegando;

    public ContactsPage()
    {
        Title = "Contactos";
        BackgroundColor = Colors.White;

        var agregar = new ToolbarItem { Text = "Agregar" };
        agregar.Clicked += async (_, _) => await AgregarContactoAsync();
        var salir = new ToolbarItem { Text = "Cerrar sesión", Order = ToolbarItemOrder.Secondary };
        salir.Clicked += async (_, _) => await CerrarSesionAsync(confirmar: true);
        ToolbarItems.Add(agregar);
        ToolbarItems.Add(salir);

        Tema.AlTocar(_nombre, AbrirPerfilAsync);
        Tema.AlTocar(_avatar, AbrirPerfilAsync);
        Tema.AlTocar(_personal, AbrirPerfilAsync);
        Tema.AlTocar(_estado, CambiarEstadoAsync);
        Tema.AlTocar(_punto, CambiarEstadoAsync);

        var info = new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center,
            Children = { _nombre, new HorizontalStackLayout { Spacing = 6, Children = { _punto, _estado } }, _personal }
        };
        var encabezado = new Grid
        {
            Padding = new Thickness(14, 12),
            ColumnSpacing = 12,
            Background = Tema.Degradado("#2E74C9", "#8DBCEB"),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }
        };
        encabezado.Add(_avatar, 0, 0);
        encabezado.Add(info, 1, 0);

        _buscar.TextChanged += (_, _) => _ = RecargarAsync();
        var barraBuscar = new Border
        {
            Margin = new Thickness(10, 8),
            Padding = new Thickness(8, 0),
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Stroke = new SolidColorBrush(Tema.Borde),
            BackgroundColor = Colors.White,
            Content = _buscar
        };

        var raiz = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        raiz.Add(encabezado, 0, 0);
        raiz.Add(barraBuscar, 0, 1);
        raiz.Add(new ScrollView { Content = _lista }, 0, 2);
        raiz.Add(_aviso, 0, 2); // encima de la lista, abajo
        Content = raiz;

        _aviso.Tocado += AbrirChatPorEmailAsync;
        Servicios.Sim.PresenciaCambio += OnPresencia;
        Servicios.Sim.MensajeRecibido += OnMensaje;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = RecargarAsync();
    }

    bool EstoyEnPantalla => Navigation.NavigationStack.LastOrDefault() == this;

    // ---------- Eventos del simulador ----------

    void OnPresencia(Contacto c, Estado anterior)
    {
        _ = RecargarAsync();
        if (EstoyEnPantalla && !Simulador.EstaEnLinea(anterior) && Simulador.EstaEnLinea(c))
            _aviso.Mostrar($"{Tema.Emoticones(c.Nombre)}\nacaba de iniciar sesión.", c.Email,
                Tema.Avatar(c.Nombre, c.Email, 40, c.Avatar));
    }

    void OnMensaje(Mensaje m, Contacto c)
    {
        _ = RecargarAsync();
        if (!EstoyEnPantalla) return;
        Tema.Vibrar(150);
        var nombre = Tema.Emoticones(c.Nombre);
        _aviso.Mostrar(m.EsZumbido
            ? $"{nombre}\nte ha enviado un zumbido."
            : $"{nombre} dice:\n{Tema.Emoticones(m.Texto)}", c.Email,
            Tema.Avatar(c.Nombre, c.Email, 40, c.Avatar));
    }

    // ---------- Lista ----------

    async Task RecargarAsync()
    {
        var u = Servicios.Usuario;
        if (u is null) return;

        _nombre.Text = Tema.Emoticones(u.Nombre);
        _estado.Text = $"({Tema.NombreEstado(u.Estado)}) ▾";
        _punto.Fill = new SolidColorBrush(Tema.ColorEstado(u.Estado));
        _personal.Text = string.IsNullOrWhiteSpace(u.MensajePersonal)
            ? "<Escribe un mensaje personal>"
            : Tema.Emoticones(u.MensajePersonal);
        _avatar.Content = Tema.Avatar(u.Nombre, u.Email, 58, u.Avatar);

        var contactos = await Servicios.Db.ContactosAsync(u.Email);
        var noLeidos = await Servicios.Db.NoLeidosAsync(u.Email);

        var filtro = _buscar.Text?.Trim() ?? "";
        if (filtro.Length > 0)
            contactos = contactos.Where(c =>
                c.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                c.Email.Contains(filtro, StringComparison.OrdinalIgnoreCase)).ToList();

        var orden = StringComparer.CurrentCultureIgnoreCase;
        var enLinea = contactos.Where(c => Simulador.EstaEnLinea(c))
            .OrderBy(c => c.Estado != Estado.Disponible).ThenBy(c => c.Nombre, orden).ToList();
        var fuera = contactos.Where(c => !Simulador.EstaEnLinea(c)).OrderBy(c => c.Nombre, orden).ToList();

        _lista.Children.Clear();
        AgregarGrupo("En línea", enLinea, noLeidos);
        AgregarGrupo("Sin conexión", fuera, noLeidos);

        if (contactos.Count == 0)
            _lista.Children.Add(new Label
            {
                Text = filtro.Length > 0 ? "Ningún contacto coincide con la búsqueda." : "Aún no tienes contactos. Toca «Agregar» para sumar uno.",
                TextColor = Tema.Gris, FontSize = 14, Padding = new Thickness(18, 16)
            });
    }

    void AgregarGrupo(string titulo, List<Contacto> lista, Dictionary<string, int> noLeidos)
    {
        bool colapsado = _colapsados.Contains(titulo);
        var cabecera = new Label
        {
            Text = $"{(colapsado ? "▸" : "▾")} {titulo} ({lista.Count})",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Tema.AzulOscuro,
            Padding = new Thickness(14, 12, 14, 4)
        };
        Tema.AlTocar(cabecera, () =>
        {
            if (!_colapsados.Remove(titulo)) _colapsados.Add(titulo);
            return RecargarAsync();
        });
        _lista.Children.Add(cabecera);
        if (colapsado) return;

        foreach (var c in lista)
            _lista.Children.Add(Fila(c, noLeidos.GetValueOrDefault(c.Email)));
    }

    View Fila(Contacto c, int sinLeer)
    {
        bool enLinea = Simulador.EstaEnLinea(c);
        string sufijo = c.Bloqueado ? " (Bloqueado)"
            : enLinea && c.Estado != Estado.Disponible ? $" ({Tema.NombreEstado(c.Estado)})" : "";

        var textos = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = Tema.Emoticones(c.Nombre) + sufijo,
                    FontSize = 15,
                    TextColor = enLinea ? Tema.Texto : Tema.Gris,
                    LineBreakMode = LineBreakMode.TailTruncation
                }
            }
        };
        if (!string.IsNullOrWhiteSpace(c.MensajePersonal))
            textos.Children.Add(new Label
            {
                Text = Tema.Emoticones(c.MensajePersonal),
                FontSize = 12,
                TextColor = Tema.Gris,
                FontAttributes = FontAttributes.Italic,
                LineBreakMode = LineBreakMode.TailTruncation
            });

        var fila = new Grid
        {
            Padding = new Thickness(14, 6, 14, 6),
            ColumnSpacing = 12,
            BackgroundColor = Colors.White,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        fila.Add(Tema.AvatarConEstado(c.Nombre, c.Email, 40, c.Bloqueado ? Estado.Desconectado : c.Estado, c.Avatar), 0, 0);
        fila.Add(textos, 1, 0);
        if (sinLeer > 0)
            fila.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#34B233"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(7, 1),
                VerticalOptions = LayoutOptions.Center,
                Content = new Label { Text = sinLeer.ToString(), TextColor = Colors.White, FontSize = 12, FontAttributes = FontAttributes.Bold }
            }, 2, 0);

        Tema.AlTocar(fila, () => AbrirChatAsync(c));
        return fila;
    }

    // ---------- Acciones ----------

    async Task AbrirChatAsync(Contacto c)
    {
        if (_navegando) return;
        _navegando = true;
        try
        {
            _aviso.Ocultar();
            await Navigation.PushAsync(new ChatPage(c));
        }
        finally { _navegando = false; }
    }

    async Task AbrirChatPorEmailAsync(string email)
    {
        var u = Servicios.Usuario;
        if (u is null) return;
        var c = (await Servicios.Db.ContactosAsync(u.Email)).FirstOrDefault(x => x.Email == email);
        if (c is not null) await AbrirChatAsync(c);
    }

    async Task CambiarEstadoAsync()
    {
        var u = Servicios.Usuario;
        if (u is null) return;
        var nombres = Tema.Seleccionables.Select(Tema.NombreEstado).ToArray();
        var r = await DisplayActionSheet("Mi estado", "Cancelar", "Cerrar sesión", nombres);
        if (r == "Cerrar sesión")
        {
            await CerrarSesionAsync(confirmar: false);
            return;
        }
        int i = Array.IndexOf(nombres, r);
        if (i < 0) return;
        u.Estado = Tema.Seleccionables[i];
        await Servicios.Db.GuardarUsuarioAsync(u);
        await RecargarAsync();
    }

    async Task AbrirPerfilAsync()
    {
        if (_navegando) return;
        _navegando = true;
        try { await Navigation.PushAsync(new PerfilPage()); }
        finally { _navegando = false; }
    }

    async Task AgregarContactoAsync()
    {
        var u = Servicios.Usuario;
        if (u is null) return;
        var email = await DisplayPromptAsync("Agregar un contacto", "Dirección de correo de tu contacto:", "Agregar", "Cancelar",
            "alguien@hotmail.com", 100, Keyboard.Email);
        if (string.IsNullOrWhiteSpace(email)) return;
        email = email.Trim().ToLowerInvariant();

        var partes = email.Split('@');
        if (partes.Length != 2 || partes[0].Length == 0 || !partes[1].Contains('.'))
        {
            await DisplayAlert("Dirección no válida", "Escribe una dirección completa, por ejemplo amigo@hotmail.com.", "Aceptar");
            return;
        }
        if (email == u.Email)
        {
            await DisplayAlert("Agregar un contacto", "No puedes agregarte a ti mismo.", "Aceptar");
            return;
        }
        var existentes = await Servicios.Db.ContactosAsync(u.Email);
        if (existentes.Any(c => c.Email == email))
        {
            await DisplayAlert("Agregar un contacto", $"{email} ya está en tu lista de contactos.", "Aceptar");
            return;
        }

        await Servicios.Db.GuardarContactoAsync(new Contacto
        {
            Propietario = u.Email,
            Email = email,
            Nombre = partes[0],
            Estado = Random.Shared.NextDouble() < 0.5 ? Estado.Disponible : Estado.Desconectado
        });
        await RecargarAsync();
        await DisplayAlert("Contacto agregado", $"{email} se agregó a tu lista de contactos.", "Aceptar");
    }

    async Task CerrarSesionAsync(bool confirmar)
    {
        if (confirmar && !await DisplayAlert("Cerrar sesión", "¿Quieres cerrar sesión?", "Cerrar sesión", "Cancelar"))
            return;
        Servicios.Sim.PresenciaCambio -= OnPresencia;
        Servicios.Sim.MensajeRecibido -= OnMensaje;

        var u = Servicios.Usuario;
        if (u?.Token is { } token)
        {
            u.Token = null;   // la siguiente vez se pide una sesión nueva
            await Servicios.Db.GuardarUsuarioAsync(u);
            _ = ApiCuentas.CerrarSesionAsync(token);   // sin esperar: si no hay internet, vence sola
        }
        Servicios.CerrarSesion();
        Application.Current!.Windows[0].Page = new LoginPage();
    }
}
