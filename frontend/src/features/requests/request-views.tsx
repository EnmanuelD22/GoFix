'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { ErrorApi, guardar, solicitar, type Cuenta } from '../auth/api';

const estados = {
  Pendiente: 'Pendiente', Aceptada: 'Aceptada', Rechazada: 'Rechazada',
  Cancelada: 'Cancelada', EnProceso: 'En proceso', Completada: 'Completada',
};

export type Opcion = { id: number; nombre: string };
export type Cobertura = { id: string; nombre: string; especialidades: Opcion[]; sectores: Opcion[] };
export type Solicitud = {
  id: string;
  tecnicoId: string;
  especialidadId: number;
  sectorId: number;
  titulo: string;
  descripcion: string;
  direccion: string;
  fechaPreferida: string;
  estado: keyof typeof estados;
  version: string;
};

type ResumenSolicitud = Pick<Solicitud, 'id' | 'titulo' | 'estado' | 'fechaPreferida'>;
type Resultados = { items: ResumenSolicitud[]; page: number; pageSize: number; total: number };

export function useConsulta<T>(ruta: string | null) {
  const [resultado, setResultado] = useState<{ ruta: string; datos?: T; error?: unknown }>();
  const [intento, setIntento] = useState(0);

  useEffect(() => {
    if (!ruta) return;
    const control = new AbortController();
    solicitar<T>(ruta, { signal: control.signal }).then(
      datos => { if (!control.signal.aborted) setResultado({ ruta, datos }); },
      error => { if (!control.signal.aborted) setResultado({ ruta, error }); },
    );
    return () => control.abort();
  }, [ruta, intento]);

  const actual = resultado?.ruta === ruta ? resultado : undefined;
  return {
    datos: actual?.datos, error: actual?.error,
    pendiente: Boolean(ruta && !actual),
    reintentar: () => { setResultado(undefined); setIntento(valor => valor + 1); },
  };
}

export function Marco({ titulo, children }: { titulo: string; children: ReactNode }) {
  return <main className="directory requests">
    <div className="directory-content">
      <nav className="directory-nav" aria-label="Navegación principal">
        <Link href="/" className="brand identity-brand">GoFix</Link>
        <Link href="/cuenta">Mi cuenta</Link>
      </nav>
      <h1>{titulo}</h1>
      {children}
    </div>
  </main>;
}

export function Fallo({ error, reintentar }: { error: unknown; reintentar?: () => void }) {
  const api = error instanceof ErrorApi ? error : undefined;
  return <div className="identity-notice" role="alert">
    {api?.status === 401 ? <p><Link href="/acceso">Inicia sesión</Link> para gestionar las solicitudes.</p>
      : <p>{api?.status === 404 ? 'La información solicitada ya no está disponible.'
        : api?.status === 403 ? 'Tu cuenta no tiene acceso a esta operación.'
        : api?.status === 409 ? 'No se pudo aplicar el cambio. Comprueba los datos actuales antes de volver a intentarlo.'
        : api?.problema.code === 'cobertura_invalida' ? 'El técnico ya no atiende la especialidad o el sector seleccionados.'
        : api?.status === 400 ? 'Revisa los datos de la solicitud.'
        : 'No se pudo completar la operación. Vuelve a intentarlo.'}</p>}
    {api?.problema.errors && <ul>{Object.entries(api.problema.errors).map(([campo, mensajes]) =>
      <li key={campo}>{mensajes.join(' ')}</li>)}</ul>}
    {reintentar && ![401, 403, 404, 400].includes(api?.status ?? 0) &&
      <button type="button" onClick={reintentar}>Volver a intentar</button>}
    {api?.problema.traceId && <small>Referencia {api.problema.traceId}</small>}
  </div>;
}

function rutaLista(rol: Cuenta['rol']) {
  return rol === 'cliente' ? '/cliente/solicitudes' : '/tecnico/solicitudes';
}

function fechaVisible(fecha: string) {
  return new Date(fecha).toLocaleString('es', { dateStyle: 'medium', timeStyle: 'short' });
}

export function ListaSolicitudes({ rol, consulta }: { rol: Cuenta['rol']; consulta: string }) {
  const cuenta = useConsulta<Cuenta>('/auth/me');
  const resultados = useConsulta<Resultados>(cuenta.datos?.rol === rol ? '/solicitudes?' + consulta : null);
  const filtros = new URLSearchParams(consulta);
  const ruta = rutaLista(rol);
  const lista = resultados.datos;

  function pagina(numero: number) {
    const siguiente = new URLSearchParams(consulta);
    siguiente.set('page', String(numero));
    return ruta + '?' + siguiente;
  }

  return <Marco titulo={rol === 'cliente' ? 'Mis solicitudes' : 'Solicitudes asignadas'}>
    {cuenta.error ? <Fallo error={cuenta.error} reintentar={cuenta.reintentar} /> : null}
    {cuenta.datos && cuenta.datos.rol !== rol && <p><Link href={rutaLista(cuenta.datos.rol)}>Ver las solicitudes de mi cuenta</Link></p>}
    {cuenta.datos?.rol === rol && <>
      {rol === 'cliente' && <p><Link href="/cliente/solicitudes/nueva">Crear solicitud</Link></p>}
      <form method="get" action={ruta} className="directory-filters" key={consulta} aria-label="Filtrar solicitudes">
        <label>Estado<select name="estado" defaultValue={filtros.get('estado') ?? ''}>
          <option value="">Todos los estados</option>
          {Object.entries(estados).map(([valor, texto]) => <option key={valor} value={valor}>{texto}</option>)}
        </select></label>
        <label>Resultados por página<select name="pageSize" defaultValue={filtros.get('pageSize') ?? '20'}>
          <option>10</option><option>20</option><option>50</option>
        </select></label>
        <button type="submit" className="identity-button">Aplicar filtros</button>
        <Link href={ruta}>Limpiar filtros</Link>
      </form>
    </>}
    {(cuenta.pendiente || resultados.pendiente) && <p role="status">Cargando solicitudes…</p>}
    {resultados.error ? <Fallo error={resultados.error} reintentar={resultados.reintentar} /> : null}
    {lista && <section aria-label="Resultados de solicitudes">
      <p role="status">{lista.total === 1 ? '1 solicitud' : lista.total + ' solicitudes'}</p>
      {lista.items.length === 0 && <p>Esta página no tiene solicitudes con los filtros seleccionados.</p>}
      <ul className="directory-results">{lista.items.map(item => <li key={item.id}>
        <h2><Link href={'/solicitudes/' + item.id}>{item.titulo}</Link></h2>
        <p>{estados[item.estado]} · {fechaVisible(item.fechaPreferida)}</p>
      </li>)}</ul>
      {(lista.page > 1 || lista.page * lista.pageSize < lista.total) && <nav className="directory-pagination" aria-label="Páginas de solicitudes">
        {lista.page > 1 && <Link href={pagina(lista.page - 1)}>Anterior</Link>}
        <span>Página {lista.page}</span>
        {lista.page * lista.pageSize < lista.total && <Link href={pagina(lista.page + 1)}>Siguiente</Link>}
      </nav>}
    </section>}
  </Marco>;
}

export function DetalleSolicitud({ id }: { id: string }) {
  const router = useRouter();
  const cuenta = useConsulta<Cuenta>('/auth/me');
  const ruta = '/solicitudes/' + encodeURIComponent(id);
  const detalle = useConsulta<Solicitud>(cuenta.datos ? ruta : null);
  const [error, setError] = useState<unknown>(null);
  const [eliminando, setEliminando] = useState(false);
  const dialogo = useRef<HTMLDialogElement>(null);
  const enCurso = useRef(false);
  const item = detalle.datos;
  const lista = cuenta.datos ? rutaLista(cuenta.datos.rol) : '/cuenta';

  async function eliminar(version: string) {
    if (enCurso.current) return;
    enCurso.current = true;
    setEliminando(true);
    setError(null);
    try {
      await guardar(ruta, 'DELETE', undefined, { 'X-Resource-Version': version });
      router.replace(lista);
    } catch (fallo) {
      setError(fallo);
      if (fallo instanceof ErrorApi && [404, 409].includes(fallo.status)) detalle.reintentar();
    } finally {
      enCurso.current = false;
      setEliminando(false);
      dialogo.current?.close();
    }
  }

  const fallo = cuenta.error ?? detalle.error;
  return <Marco titulo={item?.titulo ?? 'Detalle de solicitud'}>
    <p><Link href={lista}>Volver a solicitudes</Link></p>
    {(cuenta.pendiente || detalle.pendiente) && <p role="status">Cargando solicitud…</p>}
    {fallo ? <Fallo error={fallo} reintentar={cuenta.error ? cuenta.reintentar : detalle.reintentar} /> : null}
    {error ? <Fallo error={error} /> : null}
    {item && <div className="requests-detail">
      <dl className="identity-details">
        <div><dt>Estado</dt><dd>{estados[item.estado]}</dd></div>
        <div><dt>Descripción</dt><dd className="directory-description">{item.descripcion}</dd></div>
        <div><dt>Dirección</dt><dd>{item.direccion}</dd></div>
        <div><dt>Fecha preferida</dt><dd>{fechaVisible(item.fechaPreferida)}</dd></div>
      </dl>
      {cuenta.datos?.rol === 'cliente' && item.estado === 'Pendiente' && <div className="requests-actions">
        <Link href={ruta + '/editar'}>Editar solicitud</Link>
        <button type="button" onClick={() => dialogo.current?.showModal()}>Eliminar solicitud</button>
      </div>}
      <dialog ref={dialogo} aria-labelledby="confirmar-eliminacion" onCancel={event => { if (eliminando) event.preventDefault(); }}>
        <h2 id="confirmar-eliminacion">¿Eliminar esta solicitud?</h2>
        <p>La solicitud dejará de aparecer en tu lista y en la del técnico.</p>
        <div className="requests-actions">
          <button type="button" autoFocus disabled={eliminando} onClick={() => dialogo.current?.close()}>Conservar solicitud</button>
          <button type="button" disabled={eliminando} onClick={() => eliminar(item.version)}>{eliminando ? 'Eliminando…' : 'Confirmar eliminación'}</button>
        </div>
      </dialog>
    </div>}
  </Marco>;
}
