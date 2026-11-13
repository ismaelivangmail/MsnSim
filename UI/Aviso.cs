using Microsoft.Maui.Controls.Shapes;

namespace MsnSim.UI;

/// <summary>Ventanita emergente inferior ("X acaba de iniciar sesión").</summary>
public class Aviso : Border
{
    const double Oculto = 240;
    readonly Label _texto = new()
    {
        FontSize = 14,
        TextColor = Tema.Texto,
        MaxLines = 3,
        LineBreakMode = LineBreakMode.TailTruncation,
        VerticalOptions = LayoutOptions.Center
    };
    readonly ContentView _icono = new() { VerticalOptions = LayoutOptions.Center };
    CancellationTokenSource? _cts;

    public string? Email { get; private set; }
    public event Func<string, Task>? Tocado;

    public Aviso()
    {
        var g = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }
        };
        g.Add(_icono, 0, 0);
        g.Add(_texto, 1, 0);

        Content = g;
        Margin = new Thickness(12);
        Padding = new Thickness(14, 12);
        StrokeShape = new RoundRectangle { CornerRadius = 10 };
        Stroke = new SolidColorBrush(Tema.Borde);
        StrokeThickness = 1.5;
        Background = Tema.Degradado("#FFFFFF", "#D6E8FA");
        VerticalOptions = LayoutOptions.End;
        IsVisible = false;
        TranslationY = Oculto;
        Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Opacity = 0.25f, Radius = 12, Offset = new Point(0, 4) };

        Tema.AlTocar(this, async () =>
        {
            var email = Email;
            Ocultar();
            if (email is not null && Tocado is { } t) await t(email);
        });
    }

    public async void Mostrar(string texto, string? email, View? icono = null)
    {
        _icono.Content = icono ?? new Label { Text = "💬", FontSize = 24 };
        _texto.Text = texto;
        Email = email;
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();

        IsVisible = true;
        await this.TranslateTo(0, 0, 250, Easing.CubicOut);
        try { await Task.Delay(5000, cts.Token); }
        catch (OperationCanceledException) { return; }
        await this.TranslateTo(0, Oculto, 250, Easing.CubicIn);
        if (!cts.IsCancellationRequested) IsVisible = false;
    }

    public void Ocultar()
    {
        _cts?.Cancel();
        TranslationY = Oculto;
        IsVisible = false;
    }
}
