# Professionals / Api

Expone los catálogos públicos en `/api/v1/especialidades` y `/api/v1/sectores`. El perfil propio en `/api/v1/tecnicos/me/perfil` requiere sesión técnica y obtiene el propietario de la sesión. PUT exige antifalsificación y devuelve 409 si cambió la versión.

`GET /api/v1/tecnicos` acepta `especialidadId`, `sectorId`, `page` y `pageSize`. Los filtros son opcionales y se combinan. La página inicial es 1, el tamaño inicial 20 y el máximo 50. Entrega `items`, `page`, `pageSize` y `total`, con orden por nombre e identificador del perfil. Los identificadores de filtros deben ser positivos; una selección inexistente entrega una lista vacía.

`GET /api/v1/tecnicos/{id}` consulta el GUID del perfil técnico. Un perfil incompleto o retirado del directorio devuelve 404. La respuesta contiene `id`, `nombre`, `descripcion`, `especialidades`, `sectores`, `promedio` y `cantidadValoraciones`. Sus catálogos incluyen las opciones activas. Las respuestas excluyen los datos de acceso y contacto.
