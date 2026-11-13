# MsnSim — Messenger simulado para Android (.NET MAUI + SQLite)

App sin conexión que imita la experiencia de Messenger: inicio de sesión,
lista de contactos con presencia, conversaciones, zumbidos y emoticonos.
No usa red: un "simulador" local hace de servidor.

## Cuenta de prueba
- Correo: `ismaelivan@msn.com`
- Contraseña: `123`

La base se crea sola en el primer arranque (`msnsim.db3` en la carpeta de datos
de la app). La contraseña se guarda como SHA-256 con sal. Para reiniciar todo:
Ajustes de Android → Apps → MsnSim → Borrar datos.

## Requisitos
- Visual Studio 2022 17.14+ o VS 2026 con la carga de trabajo **.NET MAUI**
- SDK de .NET 10 y SDK de Android (lo instala VS)

## Ejecutar
Abre `MsnSim.csproj`, elige el emulador o tu teléfono (depuración USB) y F5.

## Generar el APK
```
dotnet publish -f net10.0-android -c Release
```
Queda en `bin/Release/net10.0-android/publish/com.ivanovsky.msnsim-Signed.apk`
(firmado con la clave de depuración). Instalar:
```
adb install -r bin/Release/net10.0-android/publish/com.ivanovsky.msnsim-Signed.apk
```

## Estructura
- `Data/Models.cs` — tablas `usuarios`, `contactos`, `mensajes`
- `Data/Database.cs` — sqlite-net, datos de prueba, validación de contraseña
- `Services/Simulador.cs` — presencia aleatoria, respuestas, mensajes espontáneos
- `UI/LoginPage.cs`, `UI/ContactsPage.cs`, `UI/ChatPage.cs` — pantallas (C# sin XAML)
- `UI/Aviso.cs` — ventanita emergente inferior

## Qué hace el simulador
- Cada 18–40 s un contacto cambia de estado o te escribe.
- Responde a tus mensajes según palabras clave (hola, jaja, ?, bye...) y
  muestra "está escribiendo un mensaje...".
- Los contactos Ocupados/Ausentes tardan más o no contestan.
- Si estás "No visible", nadie te inicia conversación.

## Siguiente paso
Sustituir `Simulador` por `MsnClient.Core` (ya validado contra Escargot)
manteniendo los mismos eventos: PresenciaCambio, MensajeRecibido, Escribiendo.

## Abrir Puertos en Modem

Aplicaciones>Reenvío de puertos

Nombre de la Aplicación: no mover default valores de usuario
Puerto WAN 80~80
Puerto LAN 80~80

Cliente Interno DESKTOP-SD4LIOH
Protocolo TCP
Habilitar Asociación - Mapeo


