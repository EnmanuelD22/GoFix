'use client';

import { useEffect, useState } from 'react';
import type { Cuenta } from './api';

const VIGENCIA_MS = 30_000;
let cache: { promesa: Promise<Cuenta | null>; creada: number } | null = null;

/**
 * Consulta la sesión sin redirigir: una visita pública sin cuenta es válida.
 * El resultado se comparte entre la cabecera y la página durante unos segundos.
 */
export function consultarSesion(): Promise<Cuenta | null> {
  if (cache && Date.now() - cache.creada < VIGENCIA_MS) return cache.promesa;
  const promesa = fetch('/api/v1/auth/me', {
    credentials: 'same-origin', cache: 'no-store', signal: AbortSignal.timeout(30_000),
  }).then(async respuesta => respuesta.ok ? await respuesta.json() as Cuenta : null)
    .catch(() => {
      cache = null;
      return null;
    });
  cache = { promesa, creada: Date.now() };
  return promesa;
}

/** Debe llamarse al iniciar o cerrar sesión para no mostrar un estado anterior. */
export function olvidarSesion() {
  cache = null;
}

export function useSesionOpcional() {
  const [estado, setEstado] = useState<{ cargando: boolean; cuenta: Cuenta | null }>({ cargando: true, cuenta: null });
  useEffect(() => {
    let activo = true;
    void consultarSesion().then(cuenta => { if (activo) setEstado({ cargando: false, cuenta }); });
    return () => { activo = false; };
  }, []);
  return estado;
}
