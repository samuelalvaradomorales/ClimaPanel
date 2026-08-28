# Decisiones técnicas



\## Problemas principales identificados



Durante la revisión inicial se identificaron los siguientes problemas:



\- Uso bloqueante de llamadas HTTP mediante `.Result`.

\- Creación manual de `HttpClient`.

\- Falta de propagación de `CancellationToken`.

\- Acceso a favoritos por identificador sin validar siempre el usuario propietario.

\- Prevención de favoritos duplicados basada solamente en una consulta previa.

\- Caché global que no distinguía entre ciudades.

\- Falta de protección frente a solicitudes concurrentes al proveedor meteorológico.

\- Actualización manual del pronóstico no implementada.

\- Filtrado y paginación realizados después de materializar información.

\- Exposición potencial de detalles técnicos en errores.

\- Funcionalidad de alertas meteorológicas sin implementar.



Las modificaciones se realizaron manteniendo la estructura general del proyecto y las firmas públicas solicitadas.



\## Integración HTTP, asincronía, cancelación y timeout



La integración con Open-Meteo se modificó para utilizar `HttpClient` mediante inyección de dependencias.



Los métodos de búsqueda y pronóstico utilizan operaciones asíncronas con `async/await`, eliminando llamadas bloqueantes.



El `CancellationToken` recibido por la aplicación se propaga hacia las operaciones HTTP y de deserialización.



Se configuró timeout para las solicitudes y se agregó manejo diferenciado de cancelación, timeout, errores HTTP y respuestas inválidas.



Los mensajes mostrados al usuario son genéricos, mientras que los detalles necesarios para diagnóstico se registran mediante logging.



\## Integridad y concurrencia al crear favoritos



La validación previa de duplicados se mantiene para entregar una respuesta amigable al usuario, pero no se utiliza como única garantía de integridad.



Se configuró un índice único compuesto en SQLite:



```text

(UserId, LocationId)

```



De esta manera, un mismo usuario no puede guardar dos veces la misma ciudad, incluso si dos solicitudes intentan crearla concurrentemente.



El índice incluye `UserId`, por lo que usuarios diferentes sí pueden guardar independientemente la misma ciudad.



Los conflictos de persistencia son capturados y transformados en un mensaje comprensible para el usuario.



\## Separación de datos entre usuarios



Todas las operaciones sobre favoritos por identificador validan tanto el identificador del recurso como el usuario propietario.



La consulta sigue conceptualmente la condición:



```text

Id == id \\\&\\\& UserId == userId

```



Esto se aplica a consulta, pronóstico, actualización y eliminación.



La misma estrategia se utiliza para las alertas: antes de listar, crear, activar, desactivar, evaluar o eliminar una alerta se comprueba que la ciudad favorita pertenezca al usuario actual.



De esta forma, conocer el identificador de un recurso no permite acceder a información perteneciente a otro usuario.



\## Estrategia de caché y actualización forzada



La caché meteorológica se separó por ciudad utilizando `LocationId` como parte de la clave.



Se distinguen tres orígenes de información:



\- `LIVE`: respuesta obtenida desde Open-Meteo.

\- `CACHE`: información vigente recuperada desde memoria.

\- `STALE`: último dato conocido utilizado cuando no es posible obtener información nueva.



Para evitar solicitudes externas simultáneas innecesarias se utiliza sincronización mediante `SemaphoreSlim` por `LocationId`.



Después de adquirir el bloqueo se vuelve a comprobar la caché para evitar que solicitudes concurrentes normales provoquen múltiples llamadas al proveedor.



La actualización manual utiliza `forceRefresh`, omitiendo la caché vigente y solicitando nueva información al proveedor.



Si el proveedor falla y existe información anterior disponible, se utiliza el dato `STALE` como mecanismo de resiliencia.



\## Diseño completo de alertas por umbral



Las alertas meteorológicas se persisten en SQLite y están asociadas mediante `FavoriteId` a una ciudad favorita.



Se implementaron las operaciones de:



\- creación;

\- listado;

\- activación y desactivación;

\- evaluación;

\- eliminación.



Cada ciudad admite un máximo de 5 alertas activas.



Las métricas y rangos soportados son:



\- temperatura: -80 a 80 °C;

\- humedad: 0 a 100 %;

\- precipitación: 0 a 500 mm;

\- viento: 0 a 300 km/h.



Los operadores soportados son mayor o igual (`>=`) y menor o igual (`<=`).



También se valida explícitamente que `FavoriteId` no sea `Guid.Empty`, ya que la anotación `\\\[Required]` no es suficiente para rechazar ese valor en un `Guid`.



Las alertas se evalúan al consultar el detalle de la ciudad y después de una actualización manual del pronóstico.



Se persisten:



\- estado activo/inactivo;

\- estado disparado/no disparado;

\- fecha de última evaluación;

\- fecha de último disparo.



Las operaciones que modifican alertas utilizan `POST` y protección antiforgery.



Se configuró eliminación en cascada para que las alertas asociadas sean eliminadas junto con su ciudad favorita.



\## Persistencia, consultas y paginación



Las consultas de favoritos se mantienen como `IQueryable` hasta aplicar:



\- filtro por usuario;

\- búsqueda por ciudad o país;

\- ordenamiento;

\- conteo;

\- `Skip`;

\- `Take`.



La materialización mediante `ToListAsync` ocurre después de estas operaciones.



De esta manera el filtrado y la paginación son ejecutados por SQLite en lugar de cargar todos los registros y procesarlos posteriormente en memoria.



El tamaño de página también se limita para evitar solicitudes con cantidades excesivas de registros.



\## Pruebas agregadas



Se mantuvieron y ejecutaron las pruebas automatizadas incluidas en la solución mediante:



```bash

dotnet test

```



Adicionalmente se realizaron pruebas manuales sobre los principales flujos modificados:



\- búsqueda meteorológica;

\- separación de recursos entre Ana Silva y Bruno Soto;

\- rechazo de favoritos duplicados;

\- paginación;

\- comportamiento `LIVE` y `CACHE`;

\- actualización manual;

\- creación y persistencia de alertas;

\- evaluación de una alerta con condición cumplida;

\- activación y desactivación de alertas;

\- eliminación de alertas.



\## Limitaciones conocidas y trabajo pendiente



La aplicación utiliza `EnsureCreatedAsync` para inicializar SQLite en lugar de migraciones de Entity Framework Core.



Por este motivo, cuando cambia el esquema de desarrollo es necesario recrear la base local para aplicar la nueva estructura.



La caché utilizada es en memoria y, por lo tanto, es local a una instancia de la aplicación. En un escenario distribuido sería conveniente utilizar una caché compartida.



La sincronización para evitar solicitudes meteorológicas concurrentes también es local al proceso. En una arquitectura con múltiples instancias sería necesario utilizar coordinación distribuida.



La solución se mantiene intencionalmente dentro del alcance de la prueba técnica, priorizando claridad, seguridad y cumplimiento de los requerimientos solicitados.

