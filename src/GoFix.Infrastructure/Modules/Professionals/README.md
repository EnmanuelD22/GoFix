# Professionals / Infrastructure

Persiste el perfil y su cobertura en una transacción serializable. La edición compara el rowversion y actualiza la fila del perfil incluso al cambiar solo las asociaciones. La publicación se calcula con la descripción y las opciones activas. La carga inicial está en `scripts/sql/LoadInitialCatalogs.sql`.
