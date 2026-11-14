using Microsoft.Maui.Controls.Shapes;
using MsnSim.Data;
using MsnSim.Services;

namespace MsnSim.UI;

/// <summary>Tamaño del texto, sonidos y contactos de demostración.</summary>
public class ConfiguracionPage : ContentPage
{
    readonly double _escalaInicial = Tema.Escala;
    readonly List<Button> _botonesTamano = new();
    readonly Label _muestraNombre = new() { TextColor = Tema.Gris };
    readonly Label _muestraTexto = new() { TextColor = Tema.Texto };
    readonly Label _muestraTitulo = new() { TextColor = Tema.AzulOscuro, FontAttributes = FontAttributes.Bold };
    readonly Switch _sonidos = new() { IsToggled = Alertas.SonidosActivados, OnColor = Tema.Azul, VerticalOptions = LayoutOptions.Center };
    readonly Switch _segundoPlano = new() { IsToggled = SegundoPlano.Activado, OnColor = Tema.Azul, VerticalOptions = LayoutOptions.Center };
    readonly Label _versionEstado = new() { FontSize = Tema.T(12), TextColor = Tema.Gris, HorizontalTextAlignment = TextAlignment.Center };

    public ConfiguracionPage()
    {
        Title = "Configuración";
        BackgroundColor = Colors.White;

        // --- Tamaño del texto ---
        var opciones = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
        opciones.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        opciones.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int i = 0; i < Tema.TamanosTexto.Length; i++)
        {
            var (nombre, escala) = Tema.TamanosTexto[i];
            if (i % 2 == 0) opciones.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var b = new Button
            {
                Text = nombre,
                FontSize = Math.Round(15 * escala, 1),   // cada botón ya muestra su tamaño
                CornerRadius = 8,
                BorderWidth = 1.5,
                MinimumHeightRequest = 48,
                CommandParameter = escala
            };
            b.Clicked += (_, _) => ElegirTamano(escala);
            _botonesTamano.Add(b);
            opciones.Add(b, i % 2, i / 2);
        }

        var muestra = new Border
        {
            Padding = new Thickness(14, 10),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Stroke = new SolidColorBrush(Tema.Borde),
            BackgroundColor = Tema.AzulClaro,
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children = { _muestraTitulo, _muestraNombre, _muestraTexto }
            }
        };

        // --- Sonidos ---
        _sonidos.Toggled += (_, e) => Alertas.SonidosActivados = e.Value;
        var filaSonidos = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        filaSonidos.Add(new VerticalStackLayout
        {
            Children =
            {
                new Label { Text = "Sonidos", FontSize = Tema.T(16), TextColor = Tema.Texto },
                new Label { Text = "Al recibir mensajes y zumbidos, y cuando un contacto se conecta.", FontSize = Tema.T(12), TextColor = Tema.Gris }
            }
        }, 0, 0);
        filaSonidos.Add(_sonidos, 1, 0);

        _segundoPlano.Toggled += (_, e) => SegundoPlano.Activado = e.Value;
        var filaSegundoPlano = FilaInterruptor("Seguir conectado al salir",
            "Como WhatsApp: recibes mensajes con la app cerrada. Deja una notificación fija «MsnSim está conectado».",
            _segundoPlano);

        var buscar = Tema.BotonEnlace("Buscar actualizaciones");
        buscar.Clicked += async (_, _) => await BuscarActualizacionAsync();
        _versionEstado.Text = $"MsnSim {Actualizaciones.NombreInstalado} (compilación {Actualizaciones.VersionInstalada}) · Modo en Desarrollo...";

        // --- Contactos demo ---
        var quitarDemo = Tema.BotonEnlace("Quitar contactos de demostración");
        quitarDemo.HorizontalOptions = LayoutOptions.Start;
        quitarDemo.Clicked += async (_, _) => await QuitarDemoAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20, 16, 20, 32),
                Spacing = 10,
                MaximumWidthRequest = 520,
                Children =
                {
                    Seccion("Tamaño del texto"),
                    new Label { Text = "Para leer con más comodidad. Se aplica a toda la aplicación.", FontSize = Tema.T(12), TextColor = Tema.Gris },
                    opciones,
                    muestra,
                    Separador(),
                    Seccion("Avisos"),
                    filaSonidos,
                    filaSegundoPlano,
                    Separador(),
                    Seccion("Contactos de demostración"),
                    new Label { Text = "Contactos simulados que trae una cuenta nueva. Tus contactos reales no se tocan.", FontSize = Tema.T(12), TextColor = Tema.Gris },
                    quitarDemo,
                    Separador(),
                    _versionEstado,
                    buscar
                }
            }
        };
        PintarTamano();
    }

    static Grid FilaInterruptor(string titulo, string detalle, Switch interruptor)
    {
        var g = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        g.Add(new VerticalStackLayout
        {
            Children =
            {
                new Label { Text = titulo, FontSize = Tema.T(16), TextColor = Tema.Texto },
                new Label { Text = detalle, FontSize = Tema.T(12), TextColor = Tema.Gris }
            }
        }, 0, 0);
        g.Add(interruptor, 1, 0);
        return g;
    }

    async Task BuscarActualizacionAsync()
    {
        var anterior = _versionEstado.Text;
        _versionEstado.Text = "Buscando actualizaciones...";
        var info = await Actualizaciones.BuscarAsync();
        _versionEstado.Text = anterior;
        if (info is null)
            await DisplayAlert("Actualizaciones", $"Ya tienes la versión más reciente ({Actualizaciones.NombreInstalado}).", "Aceptar");
        else
            await Actualizaciones.OfrecerAsync(this, info);
    }

    static Label Seccion(string texto) =>
        new() { Text = texto, FontSize = Tema.T(15), FontAttributes = FontAttributes.Bold, TextColor = Tema.AzulOscuro, Margin = new Thickness(0, 6, 0, 0) };

    static BoxView Separador() => new() { HeightRequest = 1, Color = Tema.Borde, Margin = new Thickness(0, 8) };

    void ElegirTamano(double escala)
    {
        Tema.CambiarEscala(escala);
        PintarTamano();
    }

    void PintarTamano()
    {
        foreach (var b in _botonesTamano)
        {
            bool elegido = Math.Abs((double)b.CommandParameter - Tema.Escala) < 0.01;
            b.BackgroundColor = elegido ? Tema.Azul : Colors.White;
            b.TextColor = elegido ? Colors.White : Tema.Texto;
            b.BorderColor = elegido ? Tema.Azul : Tema.Borde;
        }
        var nombre = Servicios.Usuario?.Nombre ?? "Tú";
        _muestraTitulo.Text = "Vista previa";
        _muestraTitulo.FontSize = Tema.T(13);
        _muestraNombre.Text = $"{Tema.Emoticones(nombre)} dice (12:30):";
        _muestraNombre.FontSize = Tema.T(12);
        _muestraTexto.Text = Tema.Emoticones("Así se verán tus mensajes :)");
        _muestraTexto.FontSize = Tema.T(15);
    }

    async Task QuitarDemoAsync()
    {
        var u = Servicios.Usuario;
        if (u is null) return;
        if (!await DisplayAlert("Contactos de demostración",
                "¿Quitar los contactos simulados y sus conversaciones? Tus contactos reales no se tocan.", "Quitar", "Cancelar"))
            return;
        await Servicios.Db.EliminarContactosDemoAsync(u.Email);
        await DisplayAlert("Contactos de demostración", "Listo, se quitaron.", "Aceptar");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Si cambió el tamaño, se reconstruye la pantalla principal para aplicarlo en todo.
        if (Math.Abs(Tema.Escala - _escalaInicial) < 0.01) return;
        if (Navigation.NavigationStack.FirstOrDefault() is ContactsPage anterior) anterior.Desconectar();
        Dispatcher.Dispatch(() => Application.Current!.Windows[0].Page = ContactsPage.CrearRaiz());
    }
}
