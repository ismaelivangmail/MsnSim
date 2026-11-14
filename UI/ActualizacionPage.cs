using MsnSim.Services;

namespace MsnSim.UI;

/// <summary>Descarga de la actualización con barra de avance.</summary>
public class ActualizacionPage : ContentPage
{
    readonly InfoVersion _info;
    readonly ProgressBar _barra = new() { ProgressColor = Tema.Azul, HeightRequest = 10 };
    readonly Label _estado = new() { FontSize = Tema.T(14), TextColor = Tema.Gris, HorizontalTextAlignment = TextAlignment.Center };
    readonly Button _boton = Tema.BotonPrincipal("Cancelar");
    readonly CancellationTokenSource _cts = new();
    string? _apk;

    public ActualizacionPage(InfoVersion info)
    {
        _info = info;
        Background = Tema.Degradado("#FFFFFF", "#D9E9FA");
        _boton.Clicked += async (_, _) => await BotonAsync();

        Content = new VerticalStackLayout
        {
            Padding = new Thickness(32, 80, 32, 32),
            Spacing = 18,
            MaximumWidthRequest = 460,
            Children =
            {
                new Label { Text = "⬇️", FontSize = 48, HorizontalTextAlignment = TextAlignment.Center },
                new Label
                {
                    Text = $"Actualizando a la versión {info.Nombre}",
                    FontSize = Tema.T(20), FontAttributes = FontAttributes.Bold, TextColor = Tema.Azul,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                _barra, _estado, _boton
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_apk is null) await DescargarAsync();
    }

    async Task DescargarAsync()
    {
        _estado.Text = "Descargando...";
        try
        {
            var avance = new Progress<double>(p =>
            {
                _barra.Progress = p;
                _estado.Text = $"Descargando... {p:P0}";
            });
            _apk = await Actualizaciones.DescargarAsync(_info, avance, _cts.Token);
            _barra.Progress = 1;
            _estado.Text = "Descarga lista. Android te pedirá confirmar la instalación.\n" +
                           "Si es la primera vez, permite «Instalar apps desconocidas» para MsnSim.";
            _boton.Text = "Instalar";
            await Actualizaciones.InstalarAsync(_apk);
        }
        catch (OperationCanceledException)
        {
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            _estado.Text = $"No se pudo descargar la actualización: {ex.Message}";
            _boton.Text = "Cerrar";
        }
    }

    async Task BotonAsync()
    {
        if (_boton.Text == "Instalar" && _apk is not null)
        {
            await Actualizaciones.InstalarAsync(_apk);
            return;
        }
        if (_apk is null && _boton.Text == "Cancelar")
        {
            _cts.Cancel();
            return;
        }
        await Navigation.PopModalAsync();
    }
}
