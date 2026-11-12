using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices;
using MsnSim.Data;

namespace MsnSim.UI;

public static class Tema
{
    public static readonly Color Azul = Color.FromArgb("#1D5FA8");
    public static readonly Color AzulOscuro = Color.FromArgb("#123F73");
    public static readonly Color AzulClaro = Color.FromArgb("#E3EFFB");
    public static readonly Color Borde = Color.FromArgb("#A9C8EA");
    public static readonly Color Gris = Color.FromArgb("#6B7280");
    public static readonly Color Texto = Color.FromArgb("#1F2937");

    public static readonly Estado[] Seleccionables =
    {
        Estado.Disponible, Estado.Ocupado, Estado.VuelvoEnseguida, Estado.Ausente,
        Estado.AlTelefono, Estado.SaliAComer, Estado.NoVisible
    };

    public static Brush Degradado(string arriba, string abajo) =>
        new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(arriba), 0f),
                new GradientStop(Color.FromArgb(abajo), 1f)
            },
            new Point(0, 0), new Point(0, 1));

    public static string NombreEstado(Estado e) => e switch
    {
        Estado.Disponible => "Disponible",
        Estado.Ocupado => "Ocupado",
        Estado.Ausente => "Ausente",
        Estado.VuelvoEnseguida => "Vuelvo enseguida",
        Estado.AlTelefono => "Al teléfono",
        Estado.SaliAComer => "Salí a comer",
        Estado.NoVisible => "No visible",
        _ => "Desconectado"
    };

    public static Color ColorEstado(Estado e) => e switch
    {
        Estado.Disponible => Color.FromArgb("#34B233"),
        Estado.Ocupado or Estado.AlTelefono => Color.FromArgb("#D93025"),
        Estado.Ausente or Estado.VuelvoEnseguida or Estado.SaliAComer => Color.FromArgb("#F29900"),
        _ => Color.FromArgb("#9CA3AF")
    };

    public static Ellipse Punto(Estado e, double tam = 12) => new()
    {
        Fill = new SolidColorBrush(ColorEstado(e)),
        Stroke = new SolidColorBrush(Colors.White),
        StrokeThickness = 1.5,
        WidthRequest = tam,
        HeightRequest = tam,
        VerticalOptions = LayoutOptions.Center
    };

    static readonly string[] Paleta = { "#1D5FA8", "#2E7D32", "#8E24AA", "#C2185B", "#EF6C00", "#00838F", "#5D4037", "#3949AB" };

    public static Color ColorDe(string clave)
    {
        int h = 0;
        foreach (var ch in clave) h = unchecked(h * 31 + ch);
        return Color.FromArgb(Paleta[(h & 0x7fffffff) % Paleta.Length]);
    }

    public static View Avatar(string nombre, string clave, double tam)
    {
        var letras = nombre.Where(char.IsLetterOrDigit).ToArray();
        var inicial = letras.Length > 0 ? char.ToUpperInvariant(letras[0]).ToString() : "?";
        return new Border
        {
            WidthRequest = tam,
            HeightRequest = tam,
            StrokeShape = new RoundRectangle { CornerRadius = tam * 0.18 },
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 2,
            Background = new SolidColorBrush(ColorDe(clave)),
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = inicial,
                TextColor = Colors.White,
                FontSize = tam * 0.45,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
    }

    // Códigos clásicos de emoticonos → emoji. Las variantes con guion van primero.
    public static readonly (string Codigo, string Emoji)[] Emotes =
    {
        (":-)", "🙂"), (":)", "🙂"), (":-D", "😃"), (":D", "😃"), (";-)", "😉"), (";)", "😉"),
        (":-(", "🙁"), (":(", "🙁"), (":-P", "😛"), (":P", "😛"), (":-O", "😮"), (":O", "😮"),
        (":'(", "😢"), (":S", "😕"), (":@", "😠"), ("(H)", "😎"), ("(L)", "❤️"), ("(U)", "💔"),
        ("(Y)", "👍"), ("(N)", "👎"), ("(K)", "💋"), ("(F)", "🌹"), ("(*)", "⭐"), ("(8)", "🎵"),
        ("(C)", "☕"), ("(^)", "🎂"), ("(G)", "🎁")
    };

    public static string Emoticones(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        foreach (var (codigo, emoji) in Emotes)
            texto = texto.Replace(codigo, emoji, StringComparison.OrdinalIgnoreCase);
        return texto;
    }

    public static string[] MenuEmoticones() =>
        Emotes.Where(e => !e.Codigo.Contains('-')).Select(e => $"{e.Emoji}   {e.Codigo}").ToArray();

    public static string? CodigoDeOpcion(string? opcion) =>
        Emotes.FirstOrDefault(e => opcion == $"{e.Emoji}   {e.Codigo}").Codigo;

    public static void Vibrar(int ms)
    {
        try
        {
            if (Vibration.Default.IsSupported)
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(ms));
        }
        catch { /* sin vibrador o sin permiso */ }
    }

    public static void AlTocar(View v, Func<Task> accion)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await accion();
        v.GestureRecognizers.Add(tap);
    }
}
