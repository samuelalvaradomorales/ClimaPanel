# ClimaPanel - prueba técnica .NET

Aplicación ASP.NET Core MVC con interfaz gráfica, Entity Framework Core, SQLite
y consumo de la API pública Open-Meteo.

## Documento oficial

Lea primero `GUIA\_POSTULANTE.pdf`. Ese documento contiene el alcance, las
reglas, los niveles evaluados, la funcionalidad nueva, la pauta y el proceso de
entrega.

## Requisitos

* SDK de .NET 8.
* Git.
* Navegador y conexión a Internet.

No necesita instalar SQLite, Docker, SQL Server ni una VPN.

## Validación inicial

En Windows:

```text
VALIDAR.cmd
```

En cualquier sistema:

```bash
dotnet restore
dotnet build
dotnet test
```

## Ejecución

```bash
dotnet run --project src/ClimaPanel.Web
```

Abra `http://localhost:5085`.

La base local se crea en `src/ClimaPanel.Web/data/climapanel.db`.

## Reinicio de la base

Windows PowerShell:

```powershell
./scripts/reset-db.ps1
```

Linux/macOS:

```bash
./scripts/reset-db.sh
```



\## Funcionalidades implementadas



La solución fue completada considerando los requerimientos indicados en la prueba técnica.



\### Integración con Open-Meteo



\- Consumo asíncrono de la API Open-Meteo.

\- Uso de `HttpClient` mediante inyección de dependencias.

\- Soporte de `CancellationToken`.

\- Control de timeout, errores HTTP y respuestas inválidas.

\- Actualización manual del pronóstico.



\### Seguridad por usuario



Las ciudades favoritas y sus alertas son privadas para cada usuario de demostración.



Todas las operaciones por identificador validan que el recurso pertenezca al usuario seleccionado antes de consultar, modificar o eliminar información.



\### Integridad de favoritos



Se evita que un mismo usuario registre dos veces la misma ciudad mediante:



\- Validación previa en la aplicación.

\- Índice único compuesto `(UserId, LocationId)` en SQLite.



Usuarios diferentes pueden guardar independientemente la misma ciudad.



\### Caché y resiliencia



El pronóstico utiliza caché independiente por ciudad.



Los datos pueden indicar los siguientes orígenes:



\- `LIVE`: información obtenida desde Open-Meteo.

\- `CACHE`: información obtenida desde la caché vigente.

\- `STALE`: último dato disponible utilizado cuando el proveedor meteorológico no puede responder.



Se utiliza sincronización por ciudad para evitar solicitudes externas simultáneas innecesarias.



\### Persistencia y paginación



Los filtros, ordenamiento, conteo y paginación de favoritos se ejecutan directamente en SQLite mediante Entity Framework Core antes de materializar los resultados.



\### Alertas meteorológicas



Cada ciudad favorita permite configurar hasta 5 alertas activas.



Métricas disponibles:



\- Temperatura: -80 a 80 °C.

\- Humedad: 0 a 100 %.

\- Precipitación: 0 a 500 mm.

\- Viento: 0 a 300 km/h.



Operadores disponibles:



\- Mayor o igual que (`>=`).

\- Menor o igual que (`<=`).



Las alertas permiten:



\- Crear.

\- Listar.

\- Activar y desactivar.

\- Evaluar automáticamente.

\- Eliminar.



La evaluación se realiza al consultar el detalle de una ciudad y al actualizar manualmente el pronóstico.



Se persisten el estado de la alerta, la fecha de última evaluación y la fecha del último disparo.



Las alertas pertenecen a la ciudad favorita y se eliminan en cascada cuando se elimina el favorito.



Las operaciones de modificación utilizan solicitudes `POST` protegidas mediante antiforgery.



\## Pruebas



Para ejecutar las pruebas automatizadas:



```bash

dotnet test

```



También se realizaron pruebas manuales sobre:



\- Separación de información entre usuarios.

\- Prevención de favoritos duplicados.

\- Actualización manual del pronóstico.

\- Uso de datos `LIVE`, `CACHE` y `STALE`.

\- Paginación de favoritos.

\- Creación y evaluación de alertas.

\- Activación, desactivación y eliminación de alertas.

\- Validación del límite máximo de alertas activas.



\## Estructura principal



\- `src/ClimaPanel.Web`: aplicación ASP.NET Core MVC.

\- `tests/ClimaPanel.Tests`: pruebas automatizadas.

\- `DECISIONS.md`: decisiones técnicas adoptadas durante la implementación.

\- `AI\_USAGE.md`: declaración del uso de herramientas de inteligencia artificial.

