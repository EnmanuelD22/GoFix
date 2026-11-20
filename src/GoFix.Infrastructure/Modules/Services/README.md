# Services / Infrastructure

El esquema `services` contiene las solicitudes, el historial y las claves de idempotencia. Una solicitud referencia el catálogo directamente para conservar sus relaciones cuando cambia la cobertura profesional. La clave `(ClienteId, Clave)` identifica un único intento de creación, y la clave foránea compuesta verifica que la solicitud pertenezca al mismo cliente.

SQL Server genera `RowVersion`. El borrado lógico requiere una solicitud pendiente y una fecha de eliminación. Las consultas operativas deberán excluir eliminadas, mientras que la lectura de idempotencia necesita distinguirlas de las inexistentes. Las columnas `datetime2(7)` representan UTC según el contrato del modelo y SQL Server no conserva `DateTime.Kind`.
