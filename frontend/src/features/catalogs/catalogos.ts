'use client';

import { useEffect, useState } from 'react';
import type { IconName } from '../../components/Icon';
import { solicitar } from '../auth/api';

export type Especialidad = { id: number; nombre: string };
export type Sector = Especialidad & { municipio: string };
export type Catalogos = { especialidades: Especialidad[]; sectores: Sector[] };

let cache: Promise<Catalogos> | null = null;

/** Los catálogos activos cambian poco: se consultan una vez por carga de la aplicación. */
export function cargarCatalogos(): Promise<Catalogos> {
  cache ??= Promise.all([
    solicitar<Especialidad[]>('/especialidades'),
    solicitar<Sector[]>('/sectores'),
  ]).then(([especialidades, sectores]) => ({ especialidades, sectores }))
    .catch((fallo: unknown) => {
      cache = null;
      throw fallo;
    });
  return cache;
}

export function useCatalogos() {
  const [estado, setEstado] = useState<{ datos?: Catalogos; error?: unknown; intento: number }>({ intento: 0 });
  useEffect(() => {
    let activo = true;
    cargarCatalogos().then(
      datos => { if (activo) setEstado(actual => ({ ...actual, datos, error: undefined })); },
      error => { if (activo) setEstado(actual => ({ ...actual, error })); },
    );
    return () => { activo = false; };
  }, [estado.intento]);
  return {
    datos: estado.datos,
    error: estado.error,
    pendiente: !estado.datos && !estado.error,
    reintentar: () => setEstado(actual => ({ intento: actual.intento + 1 })),
  };
}

export function nombreDe(lista: Especialidad[] | undefined, id: number) {
  return lista?.find(item => item.id === id)?.nombre;
}

export function estiloEspecialidad(nombre: string): { tone: string; icon: IconName } {
  const normal = nombre.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
  if (normal.includes('electric')) return { tone: 'amber', icon: 'bolt' };
  if (normal.includes('plomer')) return { tone: 'mint', icon: 'drop' };
  if (normal.includes('refriger') || normal.includes('aire acondicionado') || normal.includes('climatiz')) return { tone: 'indigo', icon: 'snow' };
  if (normal.includes('electrodom')) return { tone: 'sky', icon: 'tv' };
  if (normal.includes('cerrajer') || normal.includes('carpint')) return { tone: 'rose', icon: 'hammer' };
  if (normal.includes('pint')) return { tone: 'violet', icon: 'paint' };
  return { tone: 'indigo', icon: 'wrench' };
}
