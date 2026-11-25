# Ensayo RNF-04

El ensayo usa una base SQL Server **desechable** denominada `GoFixPerfTests_<id>`.
Nunca se ejecuta contra `GoFixDB` ni datos de usuarios. El operador crea la base,
aplica las migraciones del proyecto y ejecuta `seed.sql` con `sqlcmd -b` antes
de iniciar la API con la cadena de conexión de esa base.

Ejemplo, con la API ya iniciada por HTTPS local:

```powershell
sqlcmd -S 'lpc:.' -d 'GoFixPerfTests_<id>' -E -b -i tests/performance/seed.sql
python tests/performance/load.py --base-url https://localhost:7343 --output rendimiento.json
```

El proceso simula 20 usuarios concurrentes con una misma cuenta de cliente de
prueba durante 600 segundos y exige al
menos 10.000 peticiones. Alterna consultas públicas y creación de solicitudes
con una cuenta de prueba, excluyendo el calentamiento y el registro del
cronómetro. El informe incluye p95 de lecturas y escrituras, tasa de errores,
cantidad de peticiones, sistema operativo, procesador y CPU lógicas. Se aprueba
solo con p95 ≤800 ms en lectura, ≤1200 ms en escritura y errores <1 %.
El p95 incluye todos los intentos, también los que terminaron en error.

El resultado local de esta ejecución está en `result-2026-11-25.json`:
143.904 solicitudes HTTP, 0 errores, p95 de 253,8 ms en lectura y 104,96 ms
en escritura durante 600,11 s. Las 28.781 escrituras fueron HTTP 201. Se
eliminó la base desechable al finalizar. Esta medición valida el entorno local
registrado en el JSON, no anticipa la capacidad de otro servidor.

Guarde el JSON y anote CPU, memoria, disco y versión de SQL Server del equipo
de ensayo. El informe no demuestra prestaciones de otros equipos o ambientes.
La API debe apuntar a la misma base aislada; `load.py` verifica los 1000 perfiles
antes de empezar. Para certificados de desarrollo, el cliente confía únicamente
en el origen local indicado. Al terminar, detenga la API y elimine la base
desechable creada para el ensayo.
