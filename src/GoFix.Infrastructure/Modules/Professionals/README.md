# Professionals / Infrastructure

Persiste el perfil y su cobertura en una transacción serializable. La edición compara el rowversion y actualiza la fila del perfil incluso al cambiar solo las asociaciones. La publicación se calcula con la descripción y las opciones activas. La carga inicial está en `scripts/sql/LoadInitialCatalogs.sql`.

`EfProfessionalDirectory` pagina en SQL, ordena por nombre e identificador y proyecta los campos públicos con `AsNoTracking`. Carga las asociaciones activas por lote para los perfiles de la página. La lectura usa RepeatableRead para conservar las filas leídas durante la construcción de la ficha; las inserciones nuevas pueden aparecer al avanzar o recargar la paginación.
