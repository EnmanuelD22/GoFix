'use client';

import Link from 'next/link';
import { Logo } from '../../components/Logo';
import { Icon } from '../../components/Icon';
import { useId, useRef, useState, type FormEvent } from 'react';
import { ErrorApi, guardar, type Cuenta } from '../auth/api';
import { campoInvalido, Fallo, Marco, useConsulta, type Solicitud } from './request-views';

type Accion = 'aceptar' | 'rechazar' | 'cancelar' | 'iniciar' | 'completar';
const etiquetas: Record<Accion, string> = { aceptar: 'Aceptar solicitud', rechazar: 'Rechazar solicitud',
  cancelar: 'Cancelar solicitud', iniciar: 'Iniciar servicio', completar: 'Completar servicio' };
const tipos: Record<string, string> = { Creacion: 'Creación', Edicion: 'Edición', Transicion: 'Cambio de estado', Eliminacion: 'Eliminación' };
type Evento = { id: number; actorNombre: string; tipoEvento: string; estadoNuevo: string | null;
  createdAtUtc: string; observacion: string | null };
type Resumen = { pendientes: number; aceptadas: number; enProceso: number;
  proximasVisitas: { id: string; titulo: string; fechaConfirmada: string }[] };

function fechaVisible(fecha: string) {
  return new Date(fecha).toLocaleString('es', { dateStyle: 'medium', timeStyle: 'short' });
}

export function Seguimiento({ item, rol, recargar }: { item: Solicitud; rol: Cuenta['rol']; recargar: () => void }) {
  const avisoId = useId();
  const [accion, setAccion] = useState<Accion | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [ocupado, setOcupado] = useState(false);
  const [conflicto, setConflicto] = useState(false);
  const dialogo = useRef<HTMLDialogElement>(null);
  const enCurso = useRef(false);
  const historial = useConsulta<Evento[]>('/solicitudes/' + item.id + '/historial');
  const acciones: Accion[] = [];
  if (rol === 'tecnico' && item.estado === 'Pendiente') acciones.push('aceptar', 'rechazar');
  if ((rol === 'cliente' && item.estado === 'Pendiente') || item.estado === 'Aceptada') acciones.push('cancelar');
  if (rol === 'tecnico' && item.estado === 'Aceptada') acciones.push('iniciar');
  if (rol === 'tecnico' && item.estado === 'EnProceso') acciones.push('completar');

  function abrir(valor: Accion) {
    setAccion(valor);
    setError(null);
    dialogo.current?.showModal();
  }

  async function confirmar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!accion || enCurso.current || conflicto) return;
    const form = new FormData(event.currentTarget);
    const fecha = String(form.get('fechaConfirmada') ?? '');
    if (accion === 'aceptar' && Number.isNaN(new Date(fecha).getTime())) return;
    const datos = { version: item.version,
      ...(accion === 'aceptar' ? { fechaConfirmada: new Date(fecha).toISOString() } : {}),
      ...(['cancelar', 'rechazar'].includes(accion) ? { motivo: String(form.get('motivo')).trim() } : {}),
      ...(accion === 'completar' ? { observacion: String(form.get('observacion')).trim() } : {}) };
    enCurso.current = true;
    setOcupado(true);
    try {
      await guardar('/solicitudes/' + item.id + '/' + accion, 'POST', datos);
      dialogo.current?.close();
      recargar();
    } catch (fallo) {
      setError(fallo);
      if (fallo instanceof ErrorApi && [0, 408, 404, 409].includes(fallo.status)) setConflicto(true);
    } finally {
      enCurso.current = false;
      setOcupado(false);
    }
  }

  return <>
    <section aria-label="Seguimiento del servicio">
      {item.fechaConfirmada && <p>Visita confirmada para {fechaVisible(item.fechaConfirmada)}</p>}
      {item.inicioAtUtc && <p>Servicio iniciado el {fechaVisible(item.inicioAtUtc)}</p>}
      {item.finAtUtc && <p>Servicio completado el {fechaVisible(item.finAtUtc)}</p>}
      {item.contacto && <section aria-label="Contacto de la contraparte"><h2>Contacto</h2>
        <p>{item.contacto.nombre}</p><p><a href={'tel:' + item.contacto.telefono}>{item.contacto.telefono}</a></p>
      </section>}
      <div className="requests-actions">{acciones.map(valor =>
        <button key={valor} type="button" onClick={() => abrir(valor)}>{etiquetas[valor]}</button>)}</div>
    </section>
    <dialog ref={dialogo} aria-labelledby="accion-seguimiento" onCancel={event => { if (ocupado) event.preventDefault(); }}>
      <h2 id="accion-seguimiento">{accion ? etiquetas[accion] : 'Confirmar acción'}</h2>
      {error ? <Fallo error={error} id={avisoId} /> : null}
      {conflicto && <p><button type="button" onClick={() => { dialogo.current?.close(); recargar(); }}>Cargar la versión actual</button></p>}
      <form className="requests-form" key={accion} onSubmit={confirmar} aria-busy={ocupado} aria-describedby={error ? avisoId : undefined}>
        <fieldset disabled={ocupado || conflicto}>
          {accion === 'aceptar' && <label>Fecha y hora confirmadas<input name="fechaConfirmada" {...campoInvalido(error, "fechaConfirmada", avisoId)} type="datetime-local" required /></label>}
          {(accion === 'rechazar' || accion === 'cancelar') && <label>Motivo<textarea name="motivo" {...campoInvalido(error, "motivo", avisoId)} minLength={10} maxLength={500} rows={4} required /></label>}
          {accion === 'completar' && <label>Observación final (opcional)<textarea name="observacion" {...campoInvalido(error, "observacion", avisoId)} maxLength={1000} rows={4} /></label>}
          {accion === 'iniciar' && <p>Se registrará la fecha y hora de inicio del servicio.</p>}
          <div className="requests-actions">
            <button type="button" autoFocus onClick={() => dialogo.current?.close()}>Volver al detalle</button>
            <button type="submit" className="identity-button">{ocupado ? 'Guardando…' : 'Confirmar'}</button>
          </div>
        </fieldset>
      </form>
    </dialog>
    <section className="requests-history" aria-label="Historial de la solicitud"><h2>Historial</h2>
      {historial.pendiente && <p role="status">Cargando historial…</p>}
      {historial.error ? <Fallo error={historial.error} reintentar={historial.reintentar} /> : null}
      {historial.datos && <ol>{historial.datos.map(evento => <li key={evento.id}>
        <p><strong>{tipos[evento.tipoEvento] ?? evento.tipoEvento}</strong>{evento.estadoNuevo ? ' · ' + evento.estadoNuevo.replace('EnProceso', 'En proceso') : ''}</p>
        <p>{evento.actorNombre} · <time dateTime={evento.createdAtUtc}>{fechaVisible(evento.createdAtUtc)}</time></p>
        {evento.observacion && <p className="directory-description">{evento.observacion}</p>}
      </li>)}</ol>}
    </section>
  </>;
}

export function PanelTecnico() {
  const cuenta = useConsulta<Cuenta>('/auth/me');
  const resumen = useConsulta<Resumen>(cuenta.datos?.rol === 'tecnico' ? '/tecnicos/me/resumen' : null);
  return <div className="app-shell">
    <aside className="sidebar theme-dark">
      <Logo />
      <nav aria-label="Panel del técnico"><ul className="sidebar__list">
        <li><Link className="sidebar__link" href="/tecnico/panel" aria-current="page"><Icon name="home" size={20} />Mi agenda</Link></li>
        <li><Link className="sidebar__link" href="/tecnico/solicitudes"><Icon name="list" size={20} />Solicitudes recibidas</Link></li>
        <li><Link className="sidebar__link" href="/tecnico/perfil"><Icon name="wrench" size={20} />Mi perfil profesional</Link></li>
        <li><Link className="sidebar__link" href="/cuenta"><Icon name="user" size={20} />Mi cuenta</Link></li>
        <li><Link className="sidebar__link" href="/tecnicos"><Icon name="search" size={20} />Explorar técnicos</Link></li>
      </ul></nav>
    </aside>
    <main className="app-main">
      <header className="app-header"><div><h1>Mi agenda de servicios</h1><p className="muted">Tus solicitudes y próximas visitas, en un solo lugar.</p></div>
        <Link className="btn btn--ghost" href="/cuenta">Gestionar mi cuenta</Link>
      </header>
      {cuenta.error ? <Fallo error={cuenta.error} reintentar={cuenta.reintentar} /> : null}
      {cuenta.datos?.rol === 'cliente' && <p><Link href="/cliente/solicitudes">Ver mis solicitudes</Link></p>}
      {(cuenta.pendiente || resumen.pendiente) && <p role="status">Cargando agenda…</p>}
      {resumen.error ? <Fallo error={resumen.error} reintentar={resumen.reintentar} /> : null}
      {resumen.datos && <>
        <section aria-label="Resumen de solicitudes"><dl className="stats">
          <div className="card stat"><span className="icon-tile" data-tone="amber"><Icon name="clock" /></span><dt className="muted">Pendientes</dt><dd><Link href="/tecnico/solicitudes?estado=Pendiente" aria-label={`${resumen.datos.pendientes} solicitudes pendientes`}>{resumen.datos.pendientes}</Link></dd></div>
          <div className="card stat"><span className="icon-tile" data-tone="mint"><Icon name="cal" /></span><dt className="muted">Aceptadas</dt><dd><Link href="/tecnico/solicitudes?estado=Aceptada" aria-label={`${resumen.datos.aceptadas} solicitudes aceptadas`}>{resumen.datos.aceptadas}</Link></dd></div>
          <div className="card stat"><span className="icon-tile" data-tone="indigo"><Icon name="play" /></span><dt className="muted">En proceso</dt><dd><Link href="/tecnico/solicitudes?estado=EnProceso" aria-label={`${resumen.datos.enProceso} servicios en proceso`}>{resumen.datos.enProceso}</Link></dd></div>
        </dl></section>
        <div className="panel-grid">
          <section className="card panel" aria-labelledby="visitas-title"><h2 id="visitas-title">Próximas visitas</h2>
            {resumen.datos.proximasVisitas.length === 0 ? <p className="muted">No tienes visitas futuras confirmadas. Revisa tus solicitudes pendientes para organizar tu agenda.</p> :
              <div className="table-scroll"><table className="data-table"><caption className="muted">Tus próximas diez visitas confirmadas</caption><thead><tr><th scope="col">Servicio</th><th scope="col">Fecha</th><th scope="col">Estado</th></tr></thead><tbody>
                {resumen.datos.proximasVisitas.map(visita => <tr key={visita.id}><th scope="row"><Link href={'/solicitudes/' + visita.id}>{visita.titulo}</Link></th><td><time dateTime={visita.fechaConfirmada}>{fechaVisible(visita.fechaConfirmada)}</time></td><td><span className="tag tag--success">Aceptada</span></td></tr>)}
              </tbody></table></div>}
          </section>
          <section className="card panel"><h2>Organiza tu trabajo</h2><p className="muted">Consulta cada solicitud para confirmar la visita, registrar el avance y completar el servicio.</p><Link className="btn btn--block" href="/tecnico/solicitudes">Todas las solicitudes asignadas</Link><p className="muted">Mantén tus especialidades y sectores al día para recibir solicitudes acordes con tu cobertura.</p><Link href="/tecnico/perfil">Editar mi perfil profesional</Link></section>
        </div>
      </>}
    </main>
  </div>;
}
