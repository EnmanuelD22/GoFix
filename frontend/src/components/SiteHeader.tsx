'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useEffect, useId, useRef, useState } from 'react';
import { useSesionOpcional } from '../features/auth/session';
import { inicioPorRol } from '../lib/navegacion';
import { Icon } from './Icon';
import { Logo } from './Logo';

const enlaces = [
  { href: '/tecnicos', texto: 'Técnicos' },
  { href: '/#como-funciona', texto: 'Cómo funciona' },
];

export function SiteHeader({ bordered = false }: { bordered?: boolean }) {
  const { cuenta, cargando } = useSesionOpcional();
  const pathname = usePathname();
  const [abierto, setAbierto] = useState(false);
  const menuId = useId();
  const boton = useRef<HTMLButtonElement>(null);

  useEffect(() => { setAbierto(false); }, [pathname]);
  useEffect(() => {
    if (!abierto) return;
    function cerrar(evento: KeyboardEvent) {
      if (evento.key !== 'Escape') return;
      setAbierto(false);
      boton.current?.focus();
    }
    document.addEventListener('keydown', cerrar);
    return () => document.removeEventListener('keydown', cerrar);
  }, [abierto]);

  const acciones = cargando
    ? <span className="site-header__loading" aria-hidden="true" />
    : cuenta ? <>
      <Link className="btn btn--ghost" href="/cuenta"><Icon name="user" size={18} />Mi cuenta</Link>
      <Link className="btn" href={inicioPorRol(cuenta.rol)}>Mi panel</Link>
    </> : <>
      <Link className="btn btn--ghost" href="/acceso">Acceder</Link>
      <Link className="btn" href="/registro">Crear cuenta</Link>
    </>;

  return <header className={'site-header' + (bordered ? ' site-header--bordered' : '') + (abierto ? ' is-open' : '')}>
    <Logo />
    <button ref={boton} type="button" className="site-header__toggle" aria-expanded={abierto} aria-controls={menuId}
      onClick={() => setAbierto(valor => !valor)}>
      <Icon name={abierto ? 'close' : 'menu'} size={22} />
      <span className="sr-only">{abierto ? 'Cerrar menú' : 'Abrir menú'}</span>
    </button>
    <div id={menuId} className="site-header__menu">
      <nav className="main-nav" aria-label="Principal"><ul className="main-nav__list">
        {enlaces.map(enlace => <li key={enlace.href}>
          <Link href={enlace.href} aria-current={pathname === enlace.href ? 'page' : undefined}>{enlace.texto}</Link>
        </li>)}
      </ul></nav>
      <div className="site-header__actions" aria-busy={cargando}>{acciones}</div>
    </div>
  </header>;
}
