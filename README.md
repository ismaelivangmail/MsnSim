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

## Firma y actualizaciones (importante)
Android sólo instala una versión nueva encima de la anterior si las dos están firmadas
con la misma llave. Por eso GitHub firma siempre con la llave de MsnSim, guardada como secreto:

1. En GitHub: tu repositorio → Settings → Secrets and variables → Actions → New repository secret.
2. Crea `ANDROID_KEYSTORE_B64` y pega el contenido completo de `ANDROID_KEYSTORE_B64.txt`.
3. Crea `ANDROID_KEYSTORE_PASS` y pega el contenido de `ANDROID_KEYSTORE_PASS.txt`.
4. Guarda `msnsim.keystore` y su contraseña en un lugar seguro. **Si se pierden, no podrás
   publicar actualizaciones**: habría que desinstalar la app en todos los teléfonos.
   Nunca subas esos archivos al repositorio.

Cada compilación usa el número de ejecución de GitHub como versión (0.1.N) y deja en
Artifacts un zip con `MsnSim.apk` y `version.json`. Para publicarla, copia los dos a la
carpeta `actualizaciones` junto a `api.php` en el servidor. Al abrir la app, cada teléfono
revisa esa carpeta y ofrece instalar la versión nueva (también desde Configuración →
Buscar actualizaciones).
