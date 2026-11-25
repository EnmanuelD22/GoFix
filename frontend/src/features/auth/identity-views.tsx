'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useRef, useState, type FormEvent, type ReactNode } from 'react';
import { AppShell, useCuenta } from '../../components/AppShell';
import { Campo } from '../../components/Campo';
import { Aviso } from '../../components/Estados';
import { Fallo } from '../../components/Fallo';
import { Icon } from '../../components/Icon';
import { Logo } from '../../components/Logo';
import { destinoSeguro, inicioPorRol } from '../../lib/navegacion';
import { guardar, type Cuenta } from './api';
import { olvidarSesion } from './session';

const etiquetas: Record<string, string> = {
  nombre: 'Nombre', correo: 'Correo electrónico', telefono: 'Teléfono',
  contrasena: 'Contraseña', rol: 'Tipo de cuenta',
};

const mensajesAcceso = {
  401: 'No se pudo iniciar sesión con esas credenciales. Revisa el correo y la contraseña; tras cinco intentos fallidos la cuenta se bloquea durante 15 minutos.',
};

function MarcoIdentidad({ titulo, subtitulo, children }: { titulo: string; subtitulo: string; children: ReactNode }) {
  return (
    <main id="contenido" tabIndex={-1} className="identity" aria-labelledby="identity-title">
      <aside className="identity-story theme-dark" aria-label="Sobre GoFix">
        <Logo />
        <div>
          <h2>Tu hogar, en buenas manos.</h2>
          <p>Encuentra técnicos de tu zona, solicita la visita y sigue cada servicio desde tu cuenta.</p>
        </div>
        <ul className="checklist">
          <li><Icon name="check" size={20} />Perfiles con cobertura y reputación</li>
          <li><Icon name="check" size={20} />Historial de cada cambio del servicio</li>
          <li><Icon name="check" size={20} />Tus datos de contacto, solo tras aceptar</li>
        </ul>
        <Link href="/tecnicos">Explorar técnicos sin cuenta <Icon name="arrow" size={18} /></Link>
      </aside>
      <div className="identity-panel">
        <div className="identity-panel__logo"><Logo /></div>
        <h1 id="identity-title">{titulo}</h1>
        <p className="muted identity-panel__lead">{subtitulo}</p>
        {children}
      </div>
    </main>
  );
}

function Contrasena({ registro, error }: { registro: boolean; error: unknown }) {
  const [visible, setVisible] = useState(false);
  return <Campo etiqueta="Contraseña" nombre="contrasena" error={error}
    ayuda={registro ? 'Usa al menos 12 caracteres, con letras y números.' : undefined}>
    {control => <div className="password-field">
      <input {...control} type={visible ? 'text' : 'password'} autoComplete={registro ? 'new-password' : 'current-password'}
        minLength={registro ? 12 : undefined} maxLength={128} required />
      <button type="button" className="password-field__toggle" aria-pressed={visible} onClick={() => setVisible(valor => !valor)}>
        <Icon name={visible ? 'eyeOff' : 'eye'} size={20} />
        <span className="sr-only">Mostrar contraseña</span>
      </button>
    </div>}
  </Campo>;
}

function Contacto({ cuenta, error }: { cuenta?: Cuenta; error: unknown }) {
  return <>
    <Campo etiqueta="Nombre completo" nombre="nombre" error={error}>
      {control => <input {...control} autoComplete="name" minLength={2} maxLength={100} defaultValue={cuenta?.nombre} required />}
    </Campo>
    <Campo etiqueta="Teléfono" nombre="telefono" error={error} ayuda="De 10 a 15 dígitos, con prefijo + opcional. Ejemplo: +18095550100.">
      {control => <input {...control} type="tel" inputMode="tel" autoComplete="tel" pattern="[+]?[0-9]{10,15}" defaultValue={cuenta?.telefono} required />}
    </Campo>
  </>;
}

function TipoDeCuenta({ inicial }: { inicial: Cuenta['rol'] }) {
  return <fieldset className="choice-group">
    <legend>Tipo de cuenta</legend>
    <div className="choice-group__options">
      <label className="choice-card">
        <input type="radio" name="rol" value="cliente" defaultChecked={inicial === 'cliente'} />
        <span className="choice-card__body"><Icon name="home" /><strong>Cliente</strong><span>Necesito un servicio técnico</span></span>
      </label>
      <label className="choice-card">
        <input type="radio" name="rol" value="tecnico" defaultChecked={inicial === 'tecnico'} />
        <span className="choice-card__body"><Icon name="wrench" /><strong>Técnico</strong><span>Ofrezco mis servicios</span></span>
      </label>
    </div>
  </fieldset>;
}

export function Acceso({ registro, cuentaCreada = false, sesionExpirada = false, volver, rolInicial = 'cliente' }: {
  registro: boolean;
  cuentaCreada?: boolean;
  sesionExpirada?: boolean;
  volver?: string;
  rolInicial?: Cuenta['rol'];
}) {
  const router = useRouter();
  const [error, setError] = useState<unknown>(null);
  const [ocupado, setOcupado] = useState(false);
  const enCurso = useRef(false);
  const destino = destinoSeguro(volver);
  const conDestino = destino ? 'volver=' + encodeURIComponent(destino) : '';

  async function enviar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (enCurso.current) return;
    enCurso.current = true;
    setOcupado(true);
    setError(null);
    const datos = Object.fromEntries(new FormData(event.currentTarget));
    try {
      const cuenta = await guardar<Cuenta>(registro ? '/auth/register' : '/auth/login', 'POST', datos);
      olvidarSesion();
      router.replace(registro ? '/acceso?registro=ok' + (conDestino ? '&' + conDestino : '')
        : destino ?? inicioPorRol(cuenta.rol));
    } catch (fallo) {
      setError(fallo);
      enCurso.current = false;
      setOcupado(false);
    }
  }

  return (
    <MarcoIdentidad titulo={registro ? 'Crea tu cuenta' : 'Inicia sesión'}
      subtitulo={registro ? 'Regístrate gratis como cliente o como técnico.' : 'Accede para gestionar tus solicitudes y servicios.'}>
      {sesionExpirada && <Aviso tono="warning" titulo="Inicia sesión para continuar">
        <p>Si ya habías entrado, tu sesión venció por seguridad. Te llevaremos de vuelta a donde estabas.</p>
      </Aviso>}
      {!sesionExpirada && destino && !cuentaCreada && <Aviso>
        <p>Inicia sesión para continuar. Te llevaremos de vuelta a donde estabas.</p>
      </Aviso>}
      {cuentaCreada && <div role="status"><Aviso tono="success" titulo="Tu cuenta está lista">
        <p>Inicia sesión con tu correo y contraseña para continuar.</p>
      </Aviso></div>}
      {error ? <Fallo error={error} etiquetas={etiquetas} mensajes={registro ? undefined : mensajesAcceso} /> : null}
      <form method="post" onSubmit={enviar} aria-busy={ocupado} className="form-card">
        <fieldset disabled={ocupado}>
          {registro && <>
            <TipoDeCuenta inicial={rolInicial} />
            <Contacto error={error} />
          </>}
          <Campo etiqueta="Correo electrónico" nombre="correo" error={error}>
            {control => <input {...control} type="email" inputMode="email" autoComplete={registro ? 'email' : 'username'} maxLength={254} required />}
          </Campo>
          <Contrasena registro={registro} error={error} />
          <button className="btn btn--block" type="submit">{ocupado
            ? <><span className="spinner" aria-hidden="true" />Espera un momento…</>
            : registro ? 'Crear cuenta' : 'Iniciar sesión'}</button>
        </fieldset>
      </form>
      {registro
        ? <p className="identity-switch">¿Ya tienes cuenta? <Link href={'/acceso' + (conDestino ? '?' + conDestino : '')}>Iniciar sesión</Link></p>
        : <p className="identity-switch">¿Primera vez en GoFix? <Link href={'/registro' + (conDestino ? '?' + conDestino : '')}>Crear cuenta</Link></p>}
    </MarcoIdentidad>
  );
}

function DatosCuenta() {
  const { cuenta, actualizar } = useCuenta();
  const [error, setError] = useState<unknown>(null);
  const [ocupado, setOcupado] = useState(false);
  const [guardado, setGuardado] = useState(false);
  const enCurso = useRef(false);

  async function editar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (enCurso.current) return;
    enCurso.current = true;
    setOcupado(true);
    setGuardado(false);
    setError(null);
    try {
      const datos = Object.fromEntries(new FormData(event.currentTarget));
      actualizar(await guardar<Cuenta>('/cuenta', 'PUT', datos));
      olvidarSesion();
      setGuardado(true);
    } catch (fallo) { setError(fallo); }
    finally { enCurso.current = false; setOcupado(false); }
  }

  return <div className="account-grid">
    <section className="card" aria-labelledby="datos-title">
      <h2 id="datos-title">Datos de contacto</h2>
      <p className="muted section-lead">El técnico o el cliente verá tu nombre y teléfono solo cuando se acepte un servicio.</p>
      {guardado && <div role="status"><Aviso tono="success"><p>Cambios guardados.</p></Aviso></div>}
      {error ? <Fallo error={error} etiquetas={etiquetas} /> : null}
      <form key={`${cuenta.nombre}\n${cuenta.telefono}`} method="post" onSubmit={editar} aria-busy={ocupado} className="stack-form">
        <fieldset disabled={ocupado}>
          <Contacto cuenta={cuenta} error={error} />
          <button className="btn" type="submit">{ocupado ? 'Guardando…' : 'Guardar cambios'}</button>
        </fieldset>
      </form>
    </section>
    <section className="card" aria-labelledby="acceso-title">
      <h2 id="acceso-title">Acceso</h2>
      <dl className="details">
        <div><dt>Correo electrónico</dt><dd>{cuenta.correo}</dd></div>
        <div><dt>Tipo de cuenta</dt><dd>{cuenta.rol === 'tecnico' ? 'Técnico' : 'Cliente'}</dd></div>
      </dl>
      <p className="muted section-lead">Por seguridad, la sesión vence a los 60 minutos. Cierra sesión al terminar si usas un equipo compartido.</p>
      <div className="quick-links">
        <Link href={inicioPorRol(cuenta.rol)}><Icon name="home" size={18} />{cuenta.rol === 'tecnico' ? 'Mi agenda de servicios' : 'Mi panel'}</Link>
        {cuenta.rol === 'tecnico' && <Link href="/tecnico/perfil"><Icon name="wrench" size={18} />Editar mi perfil profesional</Link>}
        {cuenta.rol === 'tecnico' && cuenta.perfilTecnicoId && <Link href={'/tecnicos/' + cuenta.perfilTecnicoId}><Icon name="eye" size={18} />Ver mi perfil público</Link>}
        {cuenta.rol === 'cliente' && <Link href="/tecnicos"><Icon name="search" size={18} />Buscar técnicos</Link>}
      </div>
    </section>
  </div>;
}

export function MiCuenta() {
  return <AppShell titulo="Mi cuenta" descripcion="Consulta tus datos de acceso y actualiza tu información de contacto.">
    <DatosCuenta />
  </AppShell>;
}
