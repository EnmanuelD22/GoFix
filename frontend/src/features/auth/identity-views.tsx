'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { consultarCuenta, ErrorApi, guardar, type Cuenta } from './api';

function Panel({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <main className="identity" aria-labelledby="identity-title">
      <div className="identity-panel">
        <Link href="/" className="brand identity-brand">GoFix</Link>
        <h1 id="identity-title">{titulo}</h1>
        {children}
      </div>
    </main>
  );
}

const etiquetas: Record<string, string> = {
  nombre: 'Nombre', correo: 'Correo electrónico', telefono: 'Teléfono',
  contrasena: 'Contraseña', rol: 'Tipo de cuenta',
};

function campoInvalido(error: unknown, nombre: string, ayuda?: string) {
  const invalido = error instanceof ErrorApi && Boolean(error.problema.errors?.[nombre]);
  return { 'aria-invalid': invalido || undefined, 'aria-describedby': invalido ? [ayuda, `error-${nombre}`].filter(Boolean).join(' ') : ayuda };
}

function Aviso({ error, acceso = false }: { error: unknown; acceso?: boolean }) {
  if (!error) return null;
  const api = error instanceof ErrorApi ? error : null;
  const mensaje = api?.status === 429 ? 'Hay demasiados intentos. Espera unos minutos y vuelve a intentarlo.'
    : api?.status === 401 ? (acceso ? 'No se pudo iniciar sesión con esas credenciales.' : 'Tu sesión venció. Inicia sesión de nuevo.')
    : api?.status === 400 ? 'Revisa los datos e inténtalo de nuevo.'
    : 'No se pudo completar la operación. Vuelve a intentarlo.';
  const campos = Object.entries(api?.problema.errors ?? {});
  return (
    <div className="identity-notice" role="alert">
      {mensaje}
      {campos.length > 0 && <ul>{campos.map(([campo, errores]) => (
        <li key={campo} id={`error-${campo}`}>{etiquetas[campo.toLowerCase()] ?? campo} — {errores.join(' ')}</li>
      ))}</ul>}
      {api?.status === 401 && !acceso && <p><Link href="/acceso">Iniciar sesión</Link></p>}
      {api?.problema.traceId && <small>Referencia {api.problema.traceId}</small>}
    </div>
  );
}

function Contacto({ cuenta, error }: { cuenta?: Cuenta; error: unknown }) {
  return <>
    <label>Nombre<input name="nombre" autoComplete="name" minLength={2} maxLength={100} defaultValue={cuenta?.nombre} required {...campoInvalido(error, 'nombre')} /></label>
    <label>Teléfono<input name="telefono" type="tel" autoComplete="tel" pattern="[+]?[0-9]{10,15}" defaultValue={cuenta?.telefono} required {...campoInvalido(error, 'telefono', 'telefono-ayuda')} />
      <small id="telefono-ayuda">De 10 a 15 dígitos, con prefijo + opcional.</small>
    </label>
  </>;
}

export function Acceso({ registro, cuentaCreada = false }: { registro: boolean; cuentaCreada?: boolean }) {
  const router = useRouter();
  const [error, setError] = useState<unknown>(null);
  const [ocupado, setOcupado] = useState(false);

  async function enviar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (ocupado) return;
    setOcupado(true);
    setError(null);
    const datos = Object.fromEntries(new FormData(event.currentTarget));
    try {
      await guardar(registro ? '/auth/register' : '/auth/login', 'POST', datos);
      router.replace(registro ? '/acceso?registro=ok' : '/cuenta');
    } catch (fallo) {
      setError(fallo);
      setOcupado(false);
    }
  }

  return (
    <Panel titulo={registro ? 'Crea tu cuenta' : 'Inicia sesión'}>
      {cuentaCreada && <p className="identity-success" role="status">Tu cuenta está lista. Inicia sesión para continuar.</p>}
      <Aviso error={error} acceso={!registro} />
      <form method="post" onSubmit={enviar} aria-busy={ocupado}>
        <fieldset disabled={ocupado}>
          {registro && <>
            <label>Tipo de cuenta<select name="rol" defaultValue="cliente" {...campoInvalido(error, 'rol')}>
              <option value="cliente">Cliente</option><option value="tecnico">Técnico</option>
            </select></label>
            <Contacto error={error} />
          </>}
          <label>Correo electrónico<input name="correo" type="email" autoComplete={registro ? 'email' : 'username'} maxLength={254} required {...campoInvalido(error, 'correo')} /></label>
          <label>Contraseña<input name="contrasena" type="password" autoComplete={registro ? 'new-password' : 'current-password'} minLength={registro ? 12 : undefined} maxLength={128} required {...campoInvalido(error, 'contrasena', registro ? 'contrasena-ayuda' : undefined)} />
            {registro && <small id="contrasena-ayuda">Usa al menos 12 caracteres, con letras y números.</small>}
          </label>
          <button className="identity-button">{ocupado ? 'Espera un momento…' : registro ? 'Crear cuenta' : 'Iniciar sesión'}</button>
        </fieldset>
      </form>
      {registro ? <p>¿Ya tienes cuenta? <Link href="/acceso">Iniciar sesión</Link></p>
        : <p>¿Primera vez en GoFix? <Link href="/registro">Crear cuenta</Link></p>}
    </Panel>
  );
}

export function MiCuenta() {
  const router = useRouter();
  const [cuenta, setCuenta] = useState<Cuenta | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [cargando, setCargando] = useState(true);
  const [intento, setIntento] = useState(0);
  const [ocupado, setOcupado] = useState(false);
  const [guardado, setGuardado] = useState(false);

  useEffect(() => {
    const control = new AbortController();
    consultarCuenta(control.signal).then(setCuenta).catch((fallo: unknown) => {
      if (control.signal.aborted) return;
      if (fallo instanceof ErrorApi && fallo.status === 401) router.replace('/acceso');
      else setError(fallo);
    }).finally(() => { if (!control.signal.aborted) setCargando(false); });
    return () => control.abort();
  }, [intento, router]);

  async function editar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (ocupado) return;
    setOcupado(true);
    setGuardado(false);
    setError(null);
    try {
      const datos = Object.fromEntries(new FormData(event.currentTarget));
      setCuenta(await guardar<Cuenta>('/cuenta', 'PUT', datos));
      setGuardado(true);
    } catch (fallo) { setError(fallo); }
    finally { setOcupado(false); }
  }

  async function salir() {
    if (ocupado) return;
    setOcupado(true);
    setError(null);
    try {
      await guardar('/auth/logout', 'POST');
      setCuenta(null);
      router.replace('/acceso');
    } catch (fallo) { setError(fallo); setOcupado(false); }
  }

  return (
    <Panel titulo="Mi cuenta">
      {cargando && <p role="status">Cargando tu cuenta…</p>}
      <Aviso error={error} />
      {!cuenta && !cargando && Boolean(error) && <button type="button" onClick={() => {
        setError(null); setCargando(true); setIntento(intento + 1);
      }}>Volver a intentar</button>}
      {cuenta && <>
        <dl className="identity-details">
          <div><dt>Correo electrónico</dt><dd>{cuenta.correo}</dd></div>
          <div><dt>Tipo de cuenta</dt><dd>{cuenta.rol === 'tecnico' ? 'Técnico' : 'Cliente'}</dd></div>
        </dl>
        {cuenta.rol === 'tecnico' ? <p><Link href="/tecnico/solicitudes">Solicitudes asignadas</Link></p>
          : <p><Link href="/cliente/solicitudes">Mis solicitudes</Link></p>}
        {cuenta.rol === 'tecnico' && <p><Link href="/tecnico/perfil">Editar mi perfil profesional</Link></p>}
        {guardado && <p role="status" className="identity-success">Cambios guardados.</p>}
        <form key={`${cuenta.nombre}\n${cuenta.telefono}`} method="post" onSubmit={editar} aria-busy={ocupado}>
          <fieldset disabled={ocupado}>
            <Contacto cuenta={cuenta} error={error} />
            <button className="identity-button">Guardar cambios</button>
          </fieldset>
        </form>
        <button className="identity-exit" type="button" disabled={ocupado} onClick={salir}>Cerrar sesión</button>
      </>}
    </Panel>
  );
}
