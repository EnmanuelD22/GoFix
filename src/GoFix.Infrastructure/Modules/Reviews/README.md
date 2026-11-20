# Reviews / Infrastructure

El esquema `reviews` contiene una valoración por solicitud y valida la puntuación de 1 a 5. La referencia conjunta a la solicitud garantiza que los identificadores de cliente y técnico coincidan con sus participantes. `EfReviewQuery` calcula promedio y cantidad a partir de las opiniones almacenadas para los perfiles solicitados. Redondea el promedio a una cifra decimal; el directorio entrega `promedio: null` cuando falta un resumen.
