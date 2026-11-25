'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { createContext, useContext, useRef, useState, type ReactNode } from 'react';
import { guardar, type Cuenta } from '../features/auth/api';
import { olvidarSesion } from '../features/auth/session';
import { inicioPorRol } from '../lib/navegacion';
import { useConsulta } from '../lib/use-consulta';
import { Cargando } from './Estados';
import { Fallo } from './Fallo';
import { Icon, type IconName } from './Icon';
import { Logo } from './Logo';

type Enlace = { href: string; texto: string; icono: IconName; tambien?: RegExp };
type Volver = { href: string; texto: string };

const navegacion: Record<Cuenta['rol'], Enlace[]> = {
  tecnico: [
    { href: '/tecnico/panel', texto: 'Mi agenda', icono: 'home' },
    { href: '/tecnico/solicitudes', texto: 'Solicitudes', icono: 'list', tambien: /^\/solicitudes\// },
    { href: '/tecnico/perfil', texto: 'Perfil profesional', icono: 'wrench' },
    { href: '/cuenta', texto: 'Mi cuenta', icono: 'user' },
    { href: '/tecnicos', texto: 'Directorio público', icono: 'search' },
  ],
  cliente: [
    { href: '/cliente/panel', texto: 'Mi panel', icono: 'home' },
    { href: '/cliente/solicitudes', texto: 'Mis solicitudes', icono: 'list', tambien: /^\/solicitudes\// },
    { href: '/tecnicos', texto: 'Buscar técnicos', icono: 'search' },
    { href: '/cuenta', texto: 'Mi cuenta', icono: 'user' },
  ],
};

const CuentaContext = createContext<{ cuenta: Cuenta; actualizar: (cuenta: Cuenta) => void } | null>(null);

/** Cuenta autenticada del área privada. Solo disponible dentro de AppShell. */
export function useCuenta() {
  const valor = useContext(CuentaContext);
  if (!valor) throw new Error('useCuenta debe usarse dentro de AppShell.');
  return valor;
}

function enlaceActivo(enlaces: Enlace[], ruta: string) {
  const coincidencias = enlaces.filter(enlace => ruta === enlace.href || ruta.startsWith(enlace.href + '/') || enlace.tambien?.test(ruta));
  return coincidencias.sort((a, b) => b.href.length - a.href.length)[0]?.href;
}

export function AppShell({ rol, titulo, descripcion, acciones, volver, children }: {
  /** Rol requerido. Si se omite, la pantalla admite clientes y técnicos. */
  rol?: Cuenta['rol'];
  titulo: ReactNode;
  descripcion?: ReactNode;
  acciones?: ReactNode;
  volver?: Volver | ((cuenta: Cuenta) => Volver);
  children: ReactNode;
}) {
  const router = useRouter();
  const ruta = usePathname();
  const consulta = useConsulta<Cuenta>('/auth/me');
  const [editada, setEditada] = useState<Cuenta>();
  const cuenta = editada ?? consulta.datos;
  const [saliendo, setSaliendo] = useState(false);
  const [errorSalida, setErrorSalida] = useState<unknown>(null);
  const enCurso = useRef(false);
  const enlaces = cuenta ? navegacion[cuenta.rol] : [];
  const activo = enlaceActivo(enlaces, ruta);
  const permitido = cuenta && (!rol || cuenta.rol === rol);
  const regreso = typeof volver === 'function' ? (cuenta ? volver(cuenta) : undefined) : volver;

  async function salir() {
    if (enCurso.current) return;
    enCurso.current = true;
    setSaliendo(true);
    setErrorSalida(null);
    try {
      await guardar('/auth/logout', 'POST');
      olvidarSesion();
      router.replace('/acceso');
    } catch (fallo) {
      setErrorSalida(fallo);
      enCurso.current = false;
      setSaliendo(false);
    }
  }

  return <div className="app-shell">
    <aside className="sidebar theme-dark">
      <div className="sidebar__brand"><Logo /></div>
      <nav className="sidebar__nav" aria-label="Área personal">
        {cuenta ? <ul className="sidebar__list">{enlaces.map(enlace => <li key={enlace.href}>
          <Link className="sidebar__link" href={enlace.href} aria-current={activo === enlace.href ? 'page' : undefined}>
            <Icon name={enlace.icono} size={20} />{enlace.texto}
          </Link>
        </li>)}</ul> : <div className="sidebar__placeholder" aria-hidden="true">
          {[0, 1, 2, 3].map(indice => <span key={indice} className="skeleton skeleton--nav" />)}
        </div>}
      </nav>
      {cuenta && <div className="sidebar__user">
        <span className="avatar avatar--sm" aria-hidden="true">{cuenta.nombre.trim().charAt(0).toUpperCase()}</span>
        <p className="sidebar__who"><strong>{cuenta.nombre}</strong><span>{cuenta.rol === 'tecnico' ? 'Técnico' : 'Cliente'}</span></p>
        <button className="btn btn--ghost btn--sm sidebar__logout" type="button" onClick={salir} disabled={saliendo}>
          <Icon name="logout" size={18} />{saliendo ? 'Cerrando sesión…' : 'Cerrar sesión'}
        </button>
      </div>}
    </aside>
    <main id="contenido" tabIndex={-1} className="app-main">
      {regreso && <Link className="back-link" href={regreso.href}><Icon name="back" size={18} />{regreso.texto}</Link>}
      <header className="app-header">
        <div>
          <h1>{titulo}</h1>
          {descripcion && <p className="muted">{descripcion}</p>}
        </div>
        {permitido && acciones && <div className="app-header__actions">{acciones}</div>}
      </header>
      {errorSalida ? <Fallo error={errorSalida} /> : null}
      {consulta.pendiente && <Cargando texto="Cargando tu cuenta…" filas={3} />}
      {consulta.error ? <Fallo error={consulta.error} reintentar={consulta.reintentar} /> : null}
      {cuenta && !permitido && <div className="card empty-state">
        <span className="icon-tile" data-tone="amber"><Icon name="info" /></span>
        <h2>Esta sección es para {rol === 'tecnico' ? 'técnicos' : 'clientes'}</h2>
        <p className="muted">Tu cuenta es de {cuenta.rol === 'tecnico' ? 'técnico' : 'cliente'}. Desde tu panel tienes acceso a todo lo que necesitas.</p>
        <Link className="btn" href={inicioPorRol(cuenta.rol)}>Ir a mi panel</Link>
      </div>}
      {cuenta && permitido && <CuentaContext.Provider value={{ cuenta, actualizar: setEditada }}>{children}</CuentaContext.Provider>}
      <footer className="app-footer muted">
        <p>© {new Date().getFullYear()} GoFix · Piloto en Santo Domingo de Guzmán</p>
        <nav aria-label="Enlaces de ayuda"><ul>
          <li><Link href="/">Inicio</Link></li>
          <li><Link href="/tecnicos">Directorio</Link></li>
          <li><Link href="/#como-funciona">Cómo funciona</Link></li>
        </ul></nav>
      </footer>
    </main>
  </div>;
}
