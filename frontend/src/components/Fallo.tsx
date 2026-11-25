'use client';

import Link from 'next/link';
import { useEffect, useRef } from 'react';
import { ErrorApi } from '../features/auth/api';
import { Icon } from './Icon';

type Mensajes = Partial<Record<number, string>> & { codigos?: Record<string, string> };

export function mensajeDeError(error: unknown, mensajes: Mensajes = {}) {
  const api = error instanceof ErrorApi ? error : undefined;
  const codigo = api?.problema.code;
  if (codigo && mensajes.codigos?.[codigo]) return mensajes.codigos[codigo];
  if (codigo === 'cobertura_invalida') return 'El técnico ya no atiende la especialidad o el sector seleccionados.';
  const estado = api?.status ?? -1;
  if (mensajes[estado]) return mensajes[estado];
  switch (estado) {
    case 0: case 408: return api!.message;
    case 400: return 'Revisa los datos marcados e inténtalo de nuevo.';
    case 401: return 'Tu sesión venció. Inicia sesión de nuevo para continuar.';
    case 403: return 'Tu cuenta no tiene acceso a esta operación.';
    case 404: return 'La información solicitada ya no está disponible.';
    case 409: return 'Los datos cambiaron mientras los consultabas. Carga la versión actual antes de volver a intentarlo.';
    case 429: return 'Hay demasiados intentos. Espera un minuto y vuelve a intentarlo.';
    default: return 'No se pudo completar la operación. Vuelve a intentarlo.';
  }
}

/**
 * Aviso de error con foco programático, detalle por campo y referencia de correlación.
 * Conserva la clase identity-notice que usan las pruebas de navegador.
 */
export function Fallo({ error, reintentar, id, mensajes, etiquetas, enfocar = true, compacto = false }: {
  error: unknown;
  reintentar?: () => void;
  id?: string;
  mensajes?: Mensajes;
  etiquetas?: Record<string, string>;
  enfocar?: boolean;
  compacto?: boolean;
}) {
  const aviso = useRef<HTMLDivElement>(null);
  useEffect(() => { if (enfocar) aviso.current?.focus(); }, [error, enfocar]);
  const api = error instanceof ErrorApi ? error : undefined;
  const campos = Object.entries(api?.problema.errors ?? {});
  const recuperable = !api || ![400, 401, 403, 404].includes(api.status);
  return (
    <div ref={aviso} id={id} className={'notice notice--error identity-notice' + (compacto ? ' notice--compact' : '')}
      role="alert" tabIndex={-1}>
      <Icon name="alert" size={20} className="notice__icon" />
      <div className="notice__body">
        <p>{mensajeDeError(error, mensajes)}</p>
        {campos.length > 0 && <ul>{campos.map(([campo, errores]) => (
          <li key={campo}>{etiquetas?.[normalizarCampo(campo)] ? etiquetas[normalizarCampo(campo)] + ': ' : ''}{errores.join(' ')}</li>
        ))}</ul>}
        {api?.status === 401 && <p><Link href="/acceso">Iniciar sesión</Link></p>}
        {reintentar && recuperable && <button type="button" className="btn btn--sm btn--light" onClick={reintentar}>
          <Icon name="refresh" size={16} />Volver a intentar
        </button>}
        {api?.problema.traceId && <small>Referencia {api.problema.traceId}</small>}
      </div>
    </div>
  );
}

export function normalizarCampo(campo: string) {
  return campo.charAt(0).toLowerCase() + campo.slice(1);
}

export function erroresDeCampo(error: unknown): Record<string, string[]> {
  if (!(error instanceof ErrorApi) || !error.problema.errors) return {};
  return Object.fromEntries(Object.entries(error.problema.errors).map(([campo, valor]) => [normalizarCampo(campo), valor]));
}
