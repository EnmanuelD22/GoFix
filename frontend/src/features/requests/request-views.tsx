'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useEffect, useRef, useState } from 'react';
import { AppShell, useCuenta } from '../../components/AppShell';
import { Aviso, Cargando, Vacio } from '../../components/Estados';
import { Fallo } from '../../components/Fallo';
import { Icon } from '../../components/Icon';
import { fechaVisible, plural } from '../../lib/formato';
import { solicitudesPorRol } from '../../lib/navegacion';
import { useConsulta } from '../../lib/use-consulta';
import { ErrorApi, guardar, type Cuenta } from '../auth/api';
import { nombreDe, useCatalogos } from '../catalogs/catalogos';
import { ValorarServicio } from '../reviews/review-views';
import { estados, EstadoTag, ProgresoSolicitud, type Estado } from './estado';
import { Seguimiento } from './request-tracking';

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
  estado: Estado;
  version: string;
  createdAtUtc?: string;
  fechaConfirmada?: string | null;
  inicioAtUtc?: string | null;
  finAtUtc?: string | null;
  contacto?: { nombre: string; telefono: string } | null;
};

export type ResumenSolicitud = Pick<Solicitud, 'id' | 'titulo' | 'estado' | 'fechaPreferida' | 'especialidadId' | 'sectorId'>;
export type PaginaSolicitudes = { items: ResumenSolicitud[]; page: number; pageSize: number; total: number };

/** Tarjeta reutilizada por las listas y los paneles. */
export function TarjetaSolicitud({ item }: { item: ResumenSolicitud }) {
  const catalogos = useCatalogos();
  const especialidad = nombreDe(catalogos.datos?.especialidades, item.especialidadId);
  const sector = nombreDe(catalogos.datos?.sectores, item.sectorId);
  return <article className="request-card">
    <div className="request-card__head">
      <h2 className="request-card__title"><Link href={'/solicitudes/' + item.id}>{item.titulo}</Link></h2>
      <EstadoTag estado={item.estado} />
    </div>
    <ul className="meta-list">
      {especialidad && <li><Icon name="wrench" size={16} />{especialidad}</li>}
      {sector && <li><Icon name="pin" size={16} />{sector}</li>}
      <li><Icon name="cal" size={16} /><span className="sr-only">Fecha preferida: </span><time dateTime={item.fechaPreferida}>{fechaVisible(item.fechaPreferida)}</time></li>
    </ul>
  </article>;
}

function Lista({ rol, consulta }: { rol: Cuenta['rol']; consulta: string }) {
  const filtros = new URLSearchParams(consulta);
  const ruta = solicitudesPorRol(rol);
  const estadoActual = filtros.get('estado') ?? '';
  const resultados = useConsulta<PaginaSolicitudes>('/solicitudes' + (consulta ? '?' + consulta : ''));
  const lista = resultados.datos;
  const totalPaginas = lista ? Math.max(1, Math.ceil(lista.total / lista.pageSize)) : 1;

  function enlace(cambios: Record<string, string | null>) {
    const siguiente = new URLSearchParams(consulta);
    for (const [clave, valor] of Object.entries(cambios)) {
      if (valor) siguiente.set(clave, valor); else siguiente.delete(clave);
    }
    const texto = siguiente.toString();
    return ruta + (texto ? '?' + texto : '');
  }

  return <>
    <nav className="filter-tabs" aria-label="Filtrar por estado">
      <ul>
        <li><Link href={enlace({ estado: null, page: null })} aria-current={!estadoActual ? 'page' : undefined}>Todas</Link></li>
        {Object.entries(estados).map(([valor, texto]) => <li key={valor}>
          <Link href={enlace({ estado: valor, page: null })} aria-current={estadoActual === valor ? 'page' : undefined}>{texto}</Link>
        </li>)}
      </ul>
    </nav>
    <section aria-labelledby="lista-title" aria-busy={resultados.pendiente || resultados.recargando}>
      <h2 id="lista-title" className="sr-only">Resultados de solicitudes</h2>
      {resultados.pendiente && <Cargando texto="Cargando solicitudes…" filas={3} />}
      {resultados.error ? <Fallo error={resultados.error} reintentar={resultados.reintentar} /> : null}
      {lista && <>
        <div className="results-bar">
          <p role="status"><strong>{plural(lista.total, 'solicitud', 'solicitudes')}</strong>
            {estadoActual && estadoActual in estados && <span className="muted"> · {estados[estadoActual as Estado]}</span>}</p>
          {totalPaginas > 1 && <p className="muted">Página {lista.page} de {totalPaginas}</p>}
        </div>
        {lista.items.length === 0 && (lista.total > 0
          ? <Vacio titulo="Esta página no tiene solicitudes" accion={{ href: enlace({ page: null }), texto: 'Ir a la primera página' }} />
          : estadoActual
            ? <Vacio titulo="No hay solicitudes con este estado" accion={{ href: ruta, texto: 'Ver todas las solicitudes' }}>Prueba con otro estado.</Vacio>
            : rol === 'cliente'
              ? <Vacio titulo="Todavía no tienes solicitudes" accion={{ href: '/tecnicos', texto: 'Buscar un técnico' }}>
                Busca un técnico de tu zona y elige «Solicitar servicio» en su perfil.</Vacio>
              : <Vacio titulo="Aún no tienes solicitudes asignadas">
                Mantén tu perfil profesional completo para aparecer en el directorio.</Vacio>)}
        <ul className="request-list">{lista.items.map(item => <li key={item.id}><TarjetaSolicitud item={item} /></li>)}</ul>
        {totalPaginas > 1 && <nav className="pagination" aria-label="Páginas de solicitudes">
          {lista.page > 1 && <Link className="btn btn--ghost" href={enlace({ page: String(lista.page - 1) })}><Icon name="back" size={18} />Anterior</Link>}
          <span className="pagination__current">Página {lista.page} de {totalPaginas}</span>
          {lista.page < totalPaginas && <Link className="btn btn--ghost" href={enlace({ page: String(lista.page + 1) })}>Siguiente<Icon name="arrow" size={18} /></Link>}
        </nav>}
      </>}
    </section>
  </>;
}

export function ListaSolicitudes({ rol, consulta }: { rol: Cuenta['rol']; consulta: string }) {
  return <AppShell rol={rol} titulo={rol === 'cliente' ? 'Mis solicitudes' : 'Solicitudes asignadas'}
    descripcion={rol === 'cliente' ? 'Consulta el estado de tus servicios y gestiona las solicitudes pendientes.'
      : 'Revisa las solicitudes que te enviaron y actualiza su estado.'}
    acciones={rol === 'cliente' ? <Link className="btn" href="/tecnicos"><Icon name="plus" size={18} />Nueva solicitud</Link> : undefined}>
    <Lista key={consulta} rol={rol} consulta={consulta} />
  </AppShell>;
}

const mensajesAccion: Record<string, string> = {
  aceptar: 'Solicitud aceptada. El contacto del cliente ya está disponible.',
  rechazar: 'Solicitud rechazada.',
  cancelar: 'Solicitud cancelada.',
  iniciar: 'Servicio iniciado.',
  completar: 'Servicio completado.',
};

function ContenidoDetalle({ detalle, ruta }: { detalle: ReturnType<typeof useConsulta<Solicitud>>; ruta: string }) {
  const router = useRouter();
  const { cuenta } = useCuenta();
  const catalogos = useCatalogos();
  const [error, setError] = useState<unknown>(null);
  const [eliminando, setEliminando] = useState(false);
  const [confirmacion, setConfirmacion] = useState<string | null>(null);
  const dialogo = useRef<HTMLDialogElement>(null);
  const confirmacionRef = useRef<HTMLDivElement>(null);
  const enCurso = useRef(false);
  const item = detalle.datos;
  const lista = solicitudesPorRol(cuenta.rol);

  useEffect(() => { if (confirmacion) confirmacionRef.current?.focus(); }, [confirmacion]);

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

  function recargar(accion?: string) {
    setConfirmacion(accion ? mensajesAccion[accion] ?? null : null);
    detalle.reintentar();
  }

  if (detalle.pendiente) return <Cargando texto="Cargando solicitud…" filas={4} />;
  if (detalle.error) return <Fallo error={detalle.error} reintentar={detalle.reintentar} />;
  if (!item) return null;

  const especialidad = nombreDe(catalogos.datos?.especialidades, item.especialidadId);
  const sector = nombreDe(catalogos.datos?.sectores, item.sectorId);
  return <div className="request-detail" aria-busy={detalle.recargando}>
    <div className="request-detail__status card">
      <div className="request-detail__status-head">
        <EstadoTag estado={item.estado} />
        {item.fechaConfirmada && ['Aceptada', 'EnProceso', 'Completada'].includes(item.estado) && <p className="muted">
          <Icon name="cal" size={16} />Visita confirmada: <time dateTime={item.fechaConfirmada}>{fechaVisible(item.fechaConfirmada)}</time>
        </p>}
      </div>
      <ProgresoSolicitud estado={item.estado} />
      {item.estado === 'Rechazada' && <p className="muted">El técnico rechazó esta solicitud. Puedes buscar otro técnico disponible.</p>}
      {item.estado === 'Cancelada' && <p className="muted">Esta solicitud se canceló. Consulta el motivo en el historial.</p>}
    </div>
    {confirmacion && <div ref={confirmacionRef} tabIndex={-1} role="status"><Aviso tono="success"><p>{confirmacion}</p></Aviso></div>}
    {error ? <Fallo error={error} /> : null}
    <div className="request-detail__grid">
      <div className="request-detail__main">
        <section className="card" aria-labelledby="datos-visita-title">
          <h2 id="datos-visita-title">Datos de la visita</h2>
          <dl className="details details--grid">
            <div><dt>Especialidad</dt><dd>{especialidad ?? '—'}</dd></div>
            <div><dt>Sector</dt><dd>{sector ?? '—'}</dd></div>
            <div><dt>Fecha preferida</dt><dd><time dateTime={item.fechaPreferida}>{fechaVisible(item.fechaPreferida)}</time></dd></div>
            <div><dt>Dirección</dt><dd>{item.direccion}</dd></div>
            {item.inicioAtUtc && <div><dt>Inicio del servicio</dt><dd>{fechaVisible(item.inicioAtUtc)}</dd></div>}
            {item.finAtUtc && <div><dt>Fin del servicio</dt><dd>{fechaVisible(item.finAtUtc)}</dd></div>}
            <div className="details__wide"><dt>Descripción</dt><dd className="directory-description">{item.descripcion}</dd></div>
          </dl>
          {cuenta.rol === 'cliente' && item.estado === 'Pendiente' && <div className="button-row">
            <Link className="btn btn--ghost" href={ruta + '/editar'}><Icon name="edit" size={18} />Editar solicitud</Link>
            <button type="button" className="btn btn--danger-ghost" onClick={() => dialogo.current?.showModal()}><Icon name="trash" size={18} />Eliminar solicitud</button>
          </div>}
        </section>
        {cuenta.rol === 'cliente' && item.estado === 'Completada' && <section className="card"><ValorarServicio key={item.id} id={item.id} /></section>}
      </div>
      <div className="request-detail__aside">
        <Seguimiento key={item.version} item={item} rol={cuenta.rol} recargar={recargar} />
      </div>
    </div>
    <dialog ref={dialogo} className="dialog" aria-labelledby="confirmar-eliminacion" aria-describedby="confirmar-eliminacion-texto"
      onCancel={event => { if (eliminando) event.preventDefault(); }}>
      <span className="icon-tile" data-tone="rose"><Icon name="trash" /></span>
      <h2 id="confirmar-eliminacion">¿Eliminar esta solicitud?</h2>
      <p id="confirmar-eliminacion-texto" className="muted">La solicitud dejará de aparecer en tu lista y en la del técnico. Esta acción no se puede deshacer.</p>
      <div className="dialog__actions">
        <button type="button" className="btn btn--ghost" autoFocus disabled={eliminando} onClick={() => dialogo.current?.close()}>Conservar solicitud</button>
        <button type="button" className="btn btn--danger" disabled={eliminando} onClick={() => eliminar(item.version)}>{eliminando ? 'Eliminando…' : 'Confirmar eliminación'}</button>
      </div>
    </dialog>
  </div>;
}

export function DetalleSolicitud({ id }: { id: string }) {
  const ruta = '/solicitudes/' + encodeURIComponent(id);
  const detalle = useConsulta<Solicitud>(ruta);
  return <AppShell titulo={detalle.datos?.titulo ?? 'Detalle de solicitud'}
    volver={cuenta => ({ href: solicitudesPorRol(cuenta.rol), texto: cuenta.rol === 'tecnico' ? 'Solicitudes asignadas' : 'Mis solicitudes' })}>
    <ContenidoDetalle detalle={detalle} ruta={ruta} />
  </AppShell>;
}
