# Shelvia

Shelvia es una herramienta de código abierto para organizar escritorios, accesos, carpetas y archivos mediante “shelves”: ventanas flotantes ancladas al escritorio con una interfaz visual compacta.

Está pensada para que cualquiera pueda usarla, revisarla y mejorarla. La idea es ayudar a la comunidad a mantener escritorios más ordenados, prácticos y fáciles de navegar.

La app arranca una sola vez por usuario, muestra un panel administrativo en bandeja del sistema y permite abrir varias shelves en paralelo. Cada shelf puede comportarse como una vista viva de una carpeta real o como una lista personalizada de elementos guardados manualmente.

## Características

- Panel administrativo oscuro para crear, renombrar, colorear y eliminar shelves.
- Icono en la bandeja del sistema con acceso al panel principal y salida de la aplicación.
- Ventanas de shelf flotantes con sombra, desenfoque y comportamiento adaptado al escritorio.
- Soporte para arrastrar y soltar archivos o carpetas dentro de una shelf.
- Modo vinculado a carpeta: la shelf sincroniza su contenido con una carpeta de destino usando `FileSystemWatcher`.
- Modo lista: si no hay carpeta de destino, la shelf conserva una lista propia de rutas a archivos o carpetas.
- Renombrado en línea de elementos desde la shelf.
- Menú contextual para abrir, añadir, renombrar y eliminar elementos.
- Control de color, opacidad y altura de título por shelf.
- Modo compacto/minificado para ocultar contenido y mostrar solo la barra superior.
- Pensado para que la comunidad pueda organizar escritorios, accesos y carpetas de forma clara y personalizable.

## Cómo funciona

Al iniciarse, Shelvia carga las shelves guardadas en `%LocalAppData%\Shelvia`. Cada shelf se guarda en su propia carpeta identificada por un GUID y contiene un archivo de metadatos XML llamado `__shelf_metadata.xml`.

Cada shelf mantiene información como:

- nombre
- posición y tamaño
- color
- opacidad
- carpeta de destino opcional
- lista de elementos guardados
- estado de bloqueo
- estado minificado
- altura del título

Si la shelf tiene una carpeta de destino válida, muestra el contenido de esa carpeta y se actualiza cuando cambian archivos o directorios. Si no tiene carpeta, usa la lista persistida en los metadatos.

## Requisitos

- Windows 10 u 11.
- .NET 8 SDK para compilar desde código fuente.
- Arquitectura x64.

## Ejecutar desde código fuente

Desde la raíz del repositorio:

```bash
dotnet restore Shelvia/Shelvia.csproj
dotnet run --project Shelvia/Shelvia.csproj
```

## Compilar

```bash
dotnet build Shelvia/Shelvia.csproj -c Release
```

## Publicar versión portable

El proyecto incluye un flujo de publicación portable para `win-x64`.

```bash
dotnet publish Shelvia/Shelvia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o publish/win-x64-portable
```

La salida quedará en `publish/win-x64-portable`.

## Uso básico

1. Ejecuta la aplicación.
2. Abre el panel administrativo desde la bandeja del sistema.
3. Crea una nueva shelf y, si quieres, asígnale una carpeta de destino.
4. Personaliza el color, la opacidad y la altura del título.
5. Arrastra archivos o carpetas a la shelf, o usa la carpeta vinculada para trabajar directamente con su contenido.

## Persistencia de datos

Shelvia guarda la configuración del usuario en:

```text
%LocalAppData%\Shelvia
```

Dentro de esa carpeta, cada shelf vive en un directorio propio. Eliminar una shelf desde la interfaz borra también su carpeta de persistencia.

## Estructura del proyecto

- `Shelvia/Program.cs`: punto de entrada de la aplicación.
- `Shelvia/AdminForm.cs`: panel principal para administrar shelves.
- `Shelvia/ShelfWindow.cs`: ventana visual de cada shelf.
- `Shelvia/Model/`: modelos y persistencia de shelves y elementos.
- `Shelvia/Util/`: utilidades de ejecución, miniaturas y extensiones.
- `Shelvia/Win32/`: integración con APIs de Windows para efectos visuales y comportamiento de ventana.

## Notas

- La app usa un mutex con nombre `Shelvia` para evitar múltiples instancias.
- Si se corrompen metadatos de una shelf, la carga ignora ese registro y continúa con las demás.
- El panel administrativo permanece en segundo plano cuando se cierra la ventana; la aplicación sigue activa en la bandeja.

