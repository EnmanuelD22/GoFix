# GoFix · Documento de especificaciones

*Visión del producto, objetivos, alcance funcional y decisiones de negocio para orientar el desarrollo de la primera versión.*

| Control documental | Definición |
|---|---|
| Código | GF-ESP-001 |
| Versión y estado | 1.0 · Revisión técnica 2 (Next.js, módulos y pruebas) · Línea base propuesta para revisión |
| Fecha | 19 de noviembre de 2026 |
| Producto | Plataforma web de conexión entre clientes y técnicos |
| Tecnología acordada | React + TypeScript · ASP.NET Core · SQL Server |
| Arquitectura acordada | Monolito modular · Clean Architecture pragmática |
| Principios acordados | SOLID · YAGNI · DRY |

> Las metas, límites y políticas identificados como propuestos requieren validación del responsable del producto. Este documento describe el diseño esperado y no acredita una implementación existente.

## 1. Resumen y cómo leer esta documentación

### 1.1 Resumen

GoFix es una aplicación web adaptable a computadora y celular donde un cliente encuentra un técnico por especialidad y sector, solicita una visita y sigue el avance del trabajo. El técnico presenta sus servicios y gestiona las solicitudes recibidas. La primera versión cubre un municipio piloto, sin pagos, chat ni mapas, y se entrega en cinco incrementos funcionales (sección 8).

### 1.2 Relación entre documentos

| Documento | Código | Responde a la pregunta |
|---|---|---|
| Especificaciones (este documento) | GF-ESP-001 | ¿Qué producto se construye, para quién y dentro de qué límites? |
| SRS | GF-SRS-001 | ¿Qué comportamiento verificable debe tener (RF, RN, RNF, casos de uso)? |
| Arquitectura | GF-ARQ-001 | ¿Cómo se implementa (capas, datos, API, seguridad, integración)? |

Ante una contradicción, el responsable del producto debe aprobar el cambio y actualizar los tres documentos antes de desarrollar la función afectada.

## 2. Propósito y contexto

GoFix facilita que una persona encuentre un técnico que atienda su especialidad y sector, solicite una visita y consulte el avance del trabajo. Ofrece al técnico un espacio para presentar sus servicios y gestionar solicitudes.

**Problema de partida.** La dispersión de contactos y conversaciones al buscar asistencia para una avería doméstica o tecnológica.

**Hipótesis de producto.** Un directorio estructurado y un seguimiento común reducen la incertidumbre sobre quién atenderá una necesidad y en qué estado se encuentra. La hipótesis deberá contrastarse con usuarios y técnicos durante el piloto.

## 3. Objetivos y resultados esperados

| ID | Objetivo | Evidencia de logro propuesta |
|---|---|---|
| O-01 | Encontrar asistencia pertinente | Un cliente localiza perfiles por especialidad y sector y puede abrir el detalle de un técnico que cumple ambos filtros. |
| O-02 | Formalizar la solicitud | El cliente registra el problema, la dirección y su preferencia de visita. El técnico recibe la solicitud completa. |
| O-03 | Hacer visible el avance | Cada transición válida conserva actor y fecha. Ambos participantes consultan el estado actualizado. |
| O-04 | Aportar referencias sobre la atención | Cada servicio completado admite una valoración de su cliente. El perfil muestra promedio y cantidad de opiniones. |
| O-05 | Entregar una base mantenible | La solución respeta las dependencias entre capas, pasa las pruebas críticas y se ejecuta siguiendo el README. |

La aceptación inicial se medirá mediante escenarios funcionales y pruebas técnicas. El número de usuarios, el tiempo real de respuesta de los técnicos y la tasa de resolución dependen de la adopción y se medirán en el piloto. No se fijan resultados comerciales sin evidencia.

## 4. Usuarios y propuesta de valor

| Actor | Necesidad y responsabilidades |
|---|---|
| Visitante | Explorar perfiles públicos y decidir si se registra. |
| Cliente | Solicitar asistencia, proporcionar información suficiente, revisar sus solicitudes y valorar una atención completada. |
| Técnico | Mantener su perfil, elegir especialidades y sectores, decidir sobre solicitudes y registrar el avance. |
| Responsable de operación | Configurar catálogos y entorno, respaldar datos y atender incidencias mediante procedimientos operativos. No introduce un panel administrativo en esta versión. |

Se propone una cuenta con un único rol público, cliente o técnico. Una cuenta de técnico necesita completar su perfil antes de aparecer en búsquedas. El perfil expresa información declarada por su titular; la verificación profesional y las acreditaciones quedan como evolución sujeta a validación.

## 5. Alcance de la primera versión

### 5.1 Incluido

- Registro, acceso y cierre de sesión, con edición de nombre y teléfono.
- Perfiles profesionales con descripción, una o más especialidades y uno o más sectores de atención.
- Búsqueda pública paginada por especialidad y sector, con perfil y valoraciones visibles.
- CRUD de solicitudes pendientes, aceptación o rechazo, inicio, finalización y cancelación previa al inicio.
- Fecha preferida del cliente y fecha confirmada por el técnico, con historial de transiciones.
- Valoración única por servicio completado, con puntuación y comentario.
### 5.2 Reservado para etapas posteriores

Pagos, chat, mapas, fotografías, avisos por correo o teléfono, verificación de credenciales, gestión de controversias y recuperación de contraseña por correo.

### 5.3 Límites propuestos

- **Cobertura.** Un municipio configurable mediante catálogos precargados.
- **Economía.** El precio y las condiciones económicas se acuerdan directamente entre las partes.
- **Contactos.** Los contactos del técnico y del cliente se revelan a los participantes tras la aceptación.
- **Agenda.** El horario confirmado es una cita registrada. El sistema no incorpora duración del trabajo ni bloqueo automático de agenda; el técnico comprueba su disponibilidad antes de aceptar. Prevenir solapamientos exigiría definir duración, desplazamiento y capacidad de atención.
## 6. Experiencia de usuario

### 6.1 Recorrido principal

| Paso | Acción y resultado |
|---|---|
| 1. Descubrimiento | El visitante busca técnicos por especialidad y sector, consulta su descripción y reputación. |
| 2. Acceso | El cliente inicia sesión. Si se registra, vuelve al perfil para continuar la solicitud. |
| 3. Solicitud | Completa título, descripción, dirección y preferencia de fecha. La plataforma crea un registro pendiente. |
| 4. Decisión | El técnico acepta y confirma fecha, o rechaza indicando motivo. La aceptación habilita el contacto entre participantes. |
| 5. Ejecución | El técnico inicia el trabajo y, al terminar, lo marca completado. El historial conserva las acciones. |
| 6. Opinión | El cliente registra de una a cinco estrellas y un comentario opcional. El perfil actualiza su promedio. |

### 6.2 Excepciones que afectan al producto

- Una solicitud puede permanecer pendiente hasta que el técnico decida o el cliente la cancele. El sistema muestra la fecha de creación y permite cancelarla. No se promete atención inmediata.
- Cancelaciones: el cliente en pendiente y cualquiera de los participantes en aceptada, siempre con motivo.
- Una vez iniciado el trabajo, el cierre corresponde al técnico.
- Las incidencias se atienden mediante el procedimiento operativo del piloto.
## 7. Pantallas y navegación

| Área | Pantallas |
|---|---|
| Pública | Inicio con buscador, resultados paginados, perfil profesional, registro e inicio de sesión. |
| Cliente | Mis solicitudes, nueva solicitud, edición pendiente, detalle e historial, confirmación de eliminación, formulario de valoración y mi cuenta. |
| Técnico | Panel con pendientes y próximas visitas, solicitudes recibidas, detalle y acciones del servicio, perfil profesional, valoraciones y mi cuenta. |

Los formularios muestran errores junto al campo, conservan datos ante errores recuperables y ofrecen retroalimentación de envío. Las vistas tienen estados de carga, vacío, error y sesión vencida. Las animaciones son discretas y respetan la preferencia de movimiento reducido.

## 8. Plan de entrega incremental

### 8.1 Incrementos y ramas

Cada incremento es una rama `feature/` creada desde `dev` actualizado. La columna de requisitos es una **asignación propuesta** derivada del SRS (el detalle está en SRS §9).

| Orden | Rama | Resultado verificable | Requisitos principales (propuesta) |
|---|---|---|---|
| 1 | `feature/usuarios-perfiles` | Acceso por rol y perfil técnico completo con catálogos. | RF-01 a RF-05 |
| 2 | `feature/busqueda-tecnicos` | Búsqueda filtrada, paginación y detalle público. | RF-06, RF-07 |
| 3 | `feature/solicitudes-servicio` | CRUD pendiente con aislamiento de datos entre usuarios. | RF-08 a RF-11, RF-24 |
| 4 | `feature/seguimiento-servicios` | Transiciones, contacto tras aceptación, historial y control de concurrencia. | RF-12 a RF-18, RF-21, RF-23 |
| 5 | `feature/valoraciones` | Valoración única, promedio y cantidad de opiniones. | RF-19, RF-20 |

RF-22 (estados de interfaz) es transversal y se aplica en cada incremento.

### 8.2 Reglas de integración (consigna académica)

- Cada rama genera tres PR con la propia rama como origen: hacia `dev`, hacia `qa` y hacia `main`, en ese orden.
- Se integra con *merge commit* y la rama se conserva hasta completar los tres destinos.
- La evidencia final incluye al menos 15 PR cerrados y todas las funcionalidades integradas en `main`.
- Este recorrido adapta el flujo a la rúbrica particular y no es el Git Flow convencional.
### 8.3 Entrega técnica y cierre

La entrega incluye código, migraciones, instrucciones de configuración, ejemplo de variables sin credenciales y pruebas de escenarios críticos. El cierre requiere:

1. Ejecutar el recorrido cliente-técnico desde una instalación limpia.
2. Comprobar permisos.
3. Restaurar un respaldo de prueba.
La fecha límite debe obtenerse de la plataforma académica.

## 9. Riesgos y decisiones pendientes

### 9.1 Riesgos y tratamiento propuesto

| Riesgo o tensión | Tratamiento propuesto |
|---|---|
| Pocos técnicos disponibles | Validar el piloto con un grupo pequeño de profesionales y sectores concretos antes de ampliar cobertura. |
| Perfil confundido con acreditación | Presentar especialidades como declaradas. Reservar cualquier distintivo de verificación para un proceso definido. |
| Aceptación sin acuerdo de horario | Exigir fecha confirmada y permitir cancelación previa al inicio. La negociación adicional ocurre por contacto directo. |
| Cierre unilateral del trabajo | Mostrar quién completó el servicio y permitir valorar al cliente. La confirmación bilateral se evaluará con el piloto. |
| Direcciones y teléfonos expuestos | Restringir respuestas de API por participante y estado. Nunca incluirlos en perfiles públicos ni registros de diagnóstico. |
| Sobrearquitectura | Cuatro proyectos backend, un contexto y módulos internos. Incorporar componentes adicionales solo con un caso actual. |

### 9.2 Decisiones

| Tipo | Decisiones |
|---|---|
| **Expresas del solicitante** | React, SQL Server, monolito modular, Clean Architecture pragmática, SOLID, YAGNI y DRY. |
| **Propuestas por validar** | Un rol por cuenta, contactos tras aceptación, sesión de 60 minutos, cancelación previa al inicio, cierre por el técnico, eliminación lógica y metas de rendimiento del SRS. |

## 10. Referencias y glosario

- **Fuente de producto principal.** Acuerdos de la conversación de planificación de GoFix, consolidados al 19/11/2026.
- **Referencia de entrega.** Tarea 3, Uso de Git y Git Flow, Programación III.
- **Sector.** Zona perteneciente a un municipio.
- **Solicitud.** Registro de necesidad dirigido a un técnico.
- **Servicio.** Ejecución de una solicitud aceptada.
- **MVP.** Primera versión funcional para validar el producto.
## Anexo A. Revisión de arquitectura

### A.1 Cambios de organización

- Se añadió un resumen (1.1) y una tabla de relación entre documentos (1.2).
- El alcance se separó en incluido, reservado y límites (5.1 a 5.3).
- Experiencia y pantallas se agruparon en las secciones 6 y 7.
- El plan de entrega se amplió con una asignación propuesta de requisitos por rama (8.1) y las reglas de integración se separaron del cierre técnico (8.2 y 8.3).
- Riesgos y decisiones se agruparon por tipo (9).
- Las referencias, términos y glosario quedaron al final (10).
- No se modificó ninguna regla, límite ni decisión.
### A.2 Observaciones

Las observaciones **no cambian la línea base**. Requieren aprobación del responsable del producto según 1.2.

| ID | Prioridad | Observación | Recomendación |
|---|---|---|---|
| ESP-H1 | Alta | El SRS declara obligatorios los 24 RF y los 12 RNF. Entre ellos hay ensayos de rendimiento y restauración, limitación de peticiones, idempotencia y comprobaciones de salud, todo dentro de una entrega académica de cinco ramas y con YAGNI como principio acordado. | Clasificar los requisitos como Debe / Debería / Podría y definir el mínimo de la entrega. Candidatos a «Debería»: RNF-03, RNF-04, RNF-09 y RF-24. La decisión es del responsable del producto. |
| ESP-H2 | Baja | El recorrido dice que, tras registrarse, el cliente «vuelve al perfil». El SRS (CU-01) indica que el registro no abre sesión automática. | Aclarar que el flujo es registro, acceso y regreso al perfil, conservando la ruta de retorno durante el acceso. |
| ESP-H3 | Media | El detalle público (RF-07, rama 2) muestra promedio y cantidad, pero la reputación nace en la rama 5. El historial se crea con las solicitudes (rama 3) y se consulta en la rama 4. | Incluir promedio y cantidad en el contrato desde la rama 2 (con «Sin valoraciones») y crear la tabla de historial en la migración de la rama 3. |

## Actualización técnica aprobada

Por instrucción del responsable del proyecto, el frontend usa Next.js con React, ejecución Node y proxy HTTPS propio. El backend conserva las cuatro capas y organiza sus módulos bajo `Modules`. Se incorporan proyectos xUnit de aplicación, HTTP e integración SQL Server. Esta actualización recoge la decisión técnica original. Las migraciones y los casos de uso descritos en el README ya están implementados; las políticas de producto propuestas siguen sujetas a validación.

## Decisiones aprobadas para el módulo 1

El responsable aprobó que el identificador público y las referencias al técnico correspondan a `PerfilTecnico.Id`, con relación única a la cuenta. Las tablas de especialidades y sectores se crearán mediante migraciones y sus valores iniciales aprobados se cargarán mediante un script SQL versionado e idempotente, ejecutado por operación. Las entradas en uso se desactivarán para conservar referencias. La selección inicial del municipio y los catálogos fue delegada y se documenta en la sección siguiente. Los módulos y migraciones descritos en el README están implementados.

## Selección del catálogo piloto

El responsable delegó la selección inicial del municipio y los catálogos. Se eligió Santo Domingo de Guzmán (Distrito Nacional), con Gazcue, Ensanche Naco, Piantini y Bella Vista. Las especialidades iniciales son electricidad, plomería, refrigeración y aire acondicionado, reparación de electrodomésticos, cerrajería y pintura. `scripts/sql/LoadInitialCatalogs.sql` registra la carga versionada e idempotente. Esta selección concreta la cobertura del piloto y permite ampliaciones posteriores por operación.
