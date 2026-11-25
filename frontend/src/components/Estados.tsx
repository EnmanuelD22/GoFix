import type { ReactNode } from 'react';
import Link from 'next/link';
import { Icon, type IconName } from './Icon';

/** Mensaje de carga anunciado a lectores de pantalla, con esqueletos opcionales. */
export function Cargando({ texto, filas = 0, className }: { texto: string; filas?: number; className?: string }) {
  return <div className={'loading' + (className ? ' ' + className : '')}>
    <p role="status" className="loading__text"><span className="spinner" aria-hidden="true" />{texto}</p>
    {filas > 0 && <div className="skeleton-list" aria-hidden="true">
      {Array.from({ length: filas }, (_, indice) => <div key={indice} className="skeleton skeleton--row" />)}
    </div>}
  </div>;
}

export function Vacio({ icono = 'inbox', titulo, children, accion }: {
  icono?: IconName; titulo: string; children?: ReactNode; accion?: { href: string; texto: string };
}) {
  return <div className="empty-state">
    <span className="icon-tile" data-tone="indigo"><Icon name={icono} /></span>
    <h2>{titulo}</h2>
    {children && <p className="muted">{children}</p>}
    {accion && <Link className="btn" href={accion.href}>{accion.texto}</Link>}
  </div>;
}

export function Aviso({ tono = 'info', children, titulo }: { tono?: 'info' | 'success' | 'warning'; titulo?: string; children: ReactNode }) {
  const icono: IconName = tono === 'success' ? 'check' : tono === 'warning' ? 'alert' : 'info';
  return <div className={'notice notice--' + tono}>
    <Icon name={icono} size={20} className="notice__icon" />
    <div className="notice__body">{titulo && <p className="notice__title">{titulo}</p>}{children}</div>
  </div>;
}
