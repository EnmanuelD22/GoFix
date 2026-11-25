'use client';

import Link from 'next/link';
import { useEffect, useRef } from 'react';
import { Icon } from '../components/Icon';
import { PublicLayout } from '../components/PublicLayout';

export default function ErrorDePagina({ error, retry }: { error: unknown; retry: () => void }) {
  const aviso = useRef<HTMLElement>(null);
  useEffect(() => {
    console.error(error);
    aviso.current?.focus();
  }, [error]);
  const referencia = error instanceof Error && 'digest' in error ? String(error.digest) : null;
  return <PublicLayout titulo="Algo no salió como esperábamos">
    <section ref={aviso} tabIndex={-1} className="card status-page" role="alert" aria-label="Error al mostrar la página">
      <span className="icon-tile" data-tone="rose"><Icon name="alert" /></span>
      <p className="muted">No pudimos mostrar esta página. Tus datos guardados no se han perdido. Vuelve a intentarlo o regresa al inicio.</p>
      <div className="button-row">
        <button className="btn" type="button" onClick={() => retry()}><Icon name="refresh" size={18} />Volver a intentar</button>
        <Link className="btn btn--ghost" href="/">Ir al inicio</Link>
      </div>
      {referencia && <small className="muted">Referencia {referencia}</small>}
    </section>
  </PublicLayout>;
}
