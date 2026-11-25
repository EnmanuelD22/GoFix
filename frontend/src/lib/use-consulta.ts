'use client';

import { useCallback, useEffect, useState } from 'react';
import { solicitar } from '../features/auth/api';

type Resultado<T> = { ruta: string; intento: number; datos?: T; error?: unknown };

/**
 * Consulta una ruta de la API y conserva los datos anteriores mientras se recarga,
 * para que la interfaz no parpadee ni pierda el foco tras una acción.
 */
export function useConsulta<T>(ruta: string | null) {
  const [resultado, setResultado] = useState<Resultado<T>>();
  const [intento, setIntento] = useState(0);

  useEffect(() => {
    if (!ruta) return;
    const control = new AbortController();
    solicitar<T>(ruta, { signal: control.signal }).then(
      datos => { if (!control.signal.aborted) setResultado({ ruta, intento, datos }); },
      error => { if (!control.signal.aborted) setResultado({ ruta, intento, error }); },
    );
    return () => control.abort();
  }, [ruta, intento]);

  const reintentar = useCallback(() => {
    // Un error se sustituye por el estado de carga; los datos válidos se conservan.
    setResultado(actual => actual?.error ? undefined : actual);
    setIntento(valor => valor + 1);
  }, []);

  const actual = resultado?.ruta === ruta ? resultado : undefined;
  return {
    datos: actual?.datos,
    error: actual?.error,
    pendiente: Boolean(ruta && !actual),
    recargando: Boolean(ruta && actual && actual.intento !== intento),
    reintentar,
  };
}
