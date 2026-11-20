# Services / Domain

`SolicitudServicio`, `HistorialSolicitud` e `IdempotenciaSolicitud` representan los datos del módulo. Los valores de `EstadoSolicitud` y `TipoEventoSolicitud` se persisten como números y se mantienen entre versiones.

Las citas conservan su desplazamiento con `DateTimeOffset`. Los instantes de auditoría y seguimiento se escriben y se interpretan en UTC. Los casos de uso incorporarán las transiciones, la autorización y la escritura atómica de la solicitud con su historial.
