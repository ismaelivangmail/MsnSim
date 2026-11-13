using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Media;
using MsnSim.Data;
using MsnSim.Services;

namespace MsnSim.UI;

/// <summary>Mi perfil: foto, nombre para mostrar y mensaje personal.</summary>
public class PerfilPage : ContentPage
{
    readonly ContentView _foto = new() { HorizontalOptions = LayoutOptions.Center };
    readonly Entry _nombre = new() { FontSize = 15, MaxLength = 60, Placeholder = "Cómo te verán tus contactos" };
    readonly Entry _mensaje = new() { FontSize = 15, MaxLength = 120, Placeholder = "Escribe un mensaje personal" };
    readonly Label _correo = new() { FontSize = 13, TextColor = Tema.Gris, HorizontalTextAlignment = TextAlignment.Center };
    readonly Label _nota = new() { FontSize = 12, TextColor = Tema.Gris, HorizontalTextAlignment = TextAlignment.Center };
    readonly Label _error = new() { TextColor = Color.FromArgb("#C62828"), FontSize = 13, IsVisible = false };
    readonly ActivityIndicator _cargando = new() { Color = Tema.Azul, IsVisible = false, HeightRequest = 32 };
    readonly Button _guardar = Tema.BotonPrincipal("Guardar");
    readonly Button _cambiarFoto = Tema.BotonEnlace("Cambiar foto");

    public PerfilPage()
    {
        Title = "Mi perfil";
        BackgroundColor = Colors.White;

        _cambiarFoto.Clicked += async (_, _) => await CambiarFotoAsync();
        Tema.AlTocar(_foto, CambiarFotoAsync);
        _guardar.Clicked += async (_, _) => await GuardarAsync();
        _mensaje.Completed += async (_, _) => await GuardarAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 24, 24, 32),
                Spacing = 6,
                MaximumWidthRequest = 460,
                Children =
                {
                    _foto, _cambiarFoto, _correo,
                    Tema.Etiqueta("Nombre para mostrar:"), Tema.Caja(_nombre),
                    Tema.Etiqueta("Mensaje personal:"), Tema.Caja(_mensaje),
                    new Label { Text = "Puedes usar emoticonos como :) (Y) (L)", FontSize = 12, TextColor = Tema.Gris },
                    _error, _cargando, _guardar,
                    _nota
                }
            }
        };
        Pintar();
    }

    static Usuario? U => Servicios.Usuario;
    static bool EsLocal => string.IsNullOrEmpty(U?.Token);

    void Pintar()
    {
        var u = U;
        if (u is null) return;
        _foto.Content = Tema.Avatar(u.Nombre, u.Email, 120, u.Avatar);
        _nombre.Text = u.Nombre;
        _mensaje.Text = u.MensajePersonal;
        _correo.Text = u.Email;
        _nota.Text = !EsLocal
            ? "Los cambios se guardan en tu cuenta y los verán tus contactos."
            : u.Email == Database.UsuarioDemo
                ? "Cuenta de prueba: los cambios se guardan sólo en este teléfono."
                : "Entraste sin conexión: los cambios se guardarán sólo en este teléfono. Para guardarlos en tu cuenta, cierra sesión y vuelve a entrar con internet.";
    }

    void Ocupado(bool si)
    {
        _cargando.IsVisible = _cargando.IsRunning = si;
        _guardar.IsEnabled = _cambiarFoto.IsEnabled = !si;
        if (si) _error.IsVisible = false;
    }

    void Error(string? texto)
    {
        _error.Text = texto ?? "Ocurrió un error.";
        _error.IsVisible = true;
    }

    async Task<bool> ManejarSesionVencidaAsync(RespuestaApi r)
    {
        if (!r.SesionVencida) return false;
        await DisplayAlert("Sesión vencida", "Tu sesión venció. Cierra sesión y vuelve a entrar para guardar cambios en tu cuenta.", "Aceptar");
        return true;
    }

    // ---------- Nombre y mensaje ----------

    async Task GuardarAsync()
    {
        var u = U;
        if (u is null) return;
        var nombre = (_nombre.Text ?? "").Trim();
        var mensaje = (_mensaje.Text ?? "").Trim();
        if (nombre.Length == 0)
        {
            Error("El nombre para mostrar no puede quedar vacío.");
            return;
        }

        Ocupado(true);
        try
        {
            if (!EsLocal)
            {
                var r = await ApiCuentas.ActualizarPerfilAsync(u.Token!, nombre, mensaje);
                if (!r.Ok)
                {
                    if (!await ManejarSesionVencidaAsync(r)) Error(r.Error);
                    return;
                }
                nombre = r.TextoUsuario("nombre") ?? nombre;
                mensaje = r.TextoUsuario("mensaje_personal") ?? mensaje;
            }
            u.Nombre = nombre;
            u.MensajePersonal = mensaje;
            await Servicios.Db.GuardarUsuarioAsync(u);
            await Navigation.PopAsync();
        }
        finally { Ocupado(false); }
    }

    // ---------- Foto ----------

    async Task CambiarFotoAsync()
    {
        var u = U;
        if (u is null) return;
        const string galeria = "Elegir de la galería", camara = "Tomar foto", quitar = "Quitar foto";
        var opciones = Avatares.Existe(u.Avatar) ? new[] { galeria, camara, quitar } : new[] { galeria, camara };
        var r = await DisplayActionSheet("Foto de perfil", "Cancelar", null, opciones);

        if (r == quitar) { await QuitarFotoAsync(); return; }
        if (r != galeria && r != camara) return;

        FileResult? foto;
        try
        {
            if (r == camara && !MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert("Cámara", "Este teléfono no permite tomar fotos desde la app.", "Aceptar");
                return;
            }
            foto = r == camara
                ? await MediaPicker.Default.CapturePhotoAsync()
                : await MediaPicker.Default.PickPhotoAsync();
        }
        catch (PermissionException)
        {
            await DisplayAlert("Permiso necesario", "Da permiso a MsnSim en Ajustes para usar la cámara o tus fotos.", "Aceptar");
            return;
        }
        if (foto is null) return;   // canceló

        Ocupado(true);
        try
        {
            byte[] jpeg;
            try { jpeg = await Avatares.PrepararAsync(foto); }
            catch (Exception ex) { Error($"No se pudo leer la foto: {ex.Message}"); return; }

            string? remoto = u.AvatarRemoto;
            if (!EsLocal)
            {
                var resp = await ApiCuentas.SubirAvatarAsync(u.Token!, jpeg);
                if (!resp.Ok)
                {
                    if (!await ManejarSesionVencidaAsync(resp)) Error(resp.Error);
                    return;
                }
                remoto = resp.TextoUsuario("avatar");
            }

            u.Avatar = await Avatares.GuardarLocalAsync(jpeg, u.Avatar);
            u.AvatarRemoto = remoto;
            await Servicios.Db.GuardarUsuarioAsync(u);
            _foto.Content = Tema.Avatar(u.Nombre, u.Email, 120, u.Avatar);
        }
        finally { Ocupado(false); }
    }

    async Task QuitarFotoAsync()
    {
        var u = U;
        if (u is null) return;
        Ocupado(true);
        try
        {
            if (!EsLocal)
            {
                var resp = await ApiCuentas.QuitarAvatarAsync(u.Token!);
                if (!resp.Ok)
                {
                    if (!await ManejarSesionVencidaAsync(resp)) Error(resp.Error);
                    return;
                }
            }
            Avatares.BorrarLocal(u.Avatar);
            u.Avatar = null;
            u.AvatarRemoto = null;
            await Servicios.Db.GuardarUsuarioAsync(u);
            _foto.Content = Tema.Avatar(u.Nombre, u.Email, 120, null);
        }
        finally { Ocupado(false); }
    }
}
