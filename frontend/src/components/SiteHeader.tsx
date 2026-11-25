"use client";

import { useEffect, useState } from 'react';
import type { Cuenta } from '../features/auth/api';
import { Logo } from './Logo';

export function SiteHeader({ bordered = false }: { bordered?: boolean }) {
  const [cuenta, setCuenta] = useState<Cuenta | null>(null);
  const [cargando, setCargando] = useState(true);
  useEffect(() => {
    const controller = new AbortController();
    // Una visita pública sin sesión es válida: no redirigir en una consulta de navegación.
    fetch('/api/v1/auth/me', { credentials: 'same-origin', cache: 'no-store', signal: controller.signal })
      .then(async response => response.ok ? await response.json() as Cuenta : null)
      .then(setCuenta)
      .catch(() => { if (!controller.signal.aborted) setCuenta(null); })
      .finally(() => { if (!controller.signal.aborted) setCargando(false); });
    return () => controller.abort();
  }, []);
  const solicitudes = cuenta?.rol === 'tecnico' ? '/tecnico/solicitudes' : '/cliente/solicitudes';
  return <header className={`site-header${bordered ? ' site-header--bordered' : ''}`}>
    <Logo />
    <nav className="main-nav" aria-label="Principal"><ul className="main-nav__list">
      <li><a href="/tecnicos">Técnicos</a></li>
      <li><a href="/#como-funciona">Cómo funciona</a></li>
      {cuenta && <li><a href={solicitudes}>Mis solicitudes</a></li>}
      {cuenta?.rol === 'tecnico' && <li><a href="/tecnico/panel">Mi panel</a></li>}
    </ul></nav>
    <div className="site-header__actions" aria-busy={cargando}>
      {cargando ? <span className="muted" role="status">Cargando sesión…</span> : cuenta ? <>
        <a className="btn btn--ghost" href={solicitudes}>Mis solicitudes</a>
        <a className="btn" href="/cuenta">Mi cuenta</a>
      </> : <>
        <a className="btn btn--ghost" href="/acceso">Acceder</a>
        <a className="btn" href="/registro">Crear cuenta</a>
      </>}
    </div>
  </header>;
}
