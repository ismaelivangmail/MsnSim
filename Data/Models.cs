using SQLite;

namespace MsnSim.Data;

public enum Estado
{
    Desconectado = 0,
    Disponible = 1,
    Ocupado = 2,
    Ausente = 3,
    VuelvoEnseguida = 4,
    AlTelefono = 5,
    SaliAComer = 6,
    NoVisible = 7
}

[Table("usuarios")]
public class Usuario
{
    [PrimaryKey] public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string MensajePersonal { get; set; } = "";
    public Estado Estado { get; set; } = Estado.Disponible;
    public DateTime UltimoAcceso { get; set; }
    public string? Token { get; set; }          // sesión del servidor (null = cuenta sólo local)
    public string? Avatar { get; set; }         // ruta del archivo en el teléfono
    public string? AvatarRemoto { get; set; }   // ruta en el servidor, p. ej. avatares/3fa9.jpg
    public bool DemoSembrado { get; set; }      // ya se le crearon los contactos de demostración
}

[Table("contactos")]
public class Contacto
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Propietario { get; set; } = "";
    public string Email { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string MensajePersonal { get; set; } = "";
    public Estado Estado { get; set; } = Estado.Desconectado;
    public bool Bloqueado { get; set; }
    public string? Avatar { get; set; }         // ruta local; null = inicial de color
    public string? AvatarRemoto { get; set; }   // ruta en el servidor (sólo contactos reales)
    public bool EsReal { get; set; }            // false = contacto de demostración (simulado)
    public string Relacion { get; set; } = "";  // reales: aceptado | pendiente | bloqueado

    [Ignore] public bool Pendiente => EsReal && Relacion == "pendiente";
}

[Table("mensajes")]
public class Mensaje
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Propietario { get; set; } = "";
    [Indexed] public string Contacto { get; set; } = "";
    public bool Enviado { get; set; }      // true = lo mandé yo
    public string Texto { get; set; } = "";
    public bool EsZumbido { get; set; }
    public bool Leido { get; set; }
    public DateTime Fecha { get; set; }
}
