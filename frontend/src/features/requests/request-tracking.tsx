'use client';

import Link from 'next/link';
import { useRef, useState, type FormEvent } from 'react';
import { ErrorApi, guardar, type Cuenta } from '../auth/api';
import { Fallo, Marco, useConsulta, type Solicitud } from './request-views';

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
      if (fallo instanceof ErrorApi && [404, 409].includes(fallo.status)) setConflicto(true);
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
      {error ? <Fallo error={error} /> : null}
      {conflicto && <p><button type="button" onClick={() => { dialogo.current?.close(); recargar(); }}>Cargar la versión actual</button></p>}
      <form className="requests-form" key={accion} onSubmit={confirmar}>
        <fieldset disabled={ocupado || conflicto}>
          {accion === 'aceptar' && <label>Fecha y hora confirmadas<input name="fechaConfirmada" type="datetime-local" required /></label>}
          {(accion === 'rechazar' || accion === 'cancelar') && <label>Motivo<textarea name="motivo" minLength={10} maxLength={500} rows={4} required /></label>}
          {accion === 'completar' && <label>Observación final (opcional)<textarea name="observacion" maxLength={1000} rows={4} /></label>}
          {accion === 'iniciar' && <p>Se registrará la fecha y hora de inicio del servicio.</p>}
          <div className="requests-actions">
            <button type="button" onClick={() => dialogo.current?.close()}>Volver al detalle</button>
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
  return <Marco titulo="Mi agenda de servicios">
    {cuenta.error ? <Fallo error={cuenta.error} reintentar={cuenta.reintentar} /> : null}
    {cuenta.datos?.rol === 'cliente' && <p><Link href="/cliente/solicitudes">Ver mis solicitudes</Link></p>}
    {(cuenta.pendiente || resumen.pendiente) && <p role="status">Cargando agenda…</p>}
    {resumen.error ? <Fallo error={resumen.error} reintentar={resumen.reintentar} /> : null}
    {resumen.datos && <>
      <p><Link href="/tecnico/solicitudes">Todas las solicitudes asignadas</Link></p>
      <ul className="requests-counts">
        <li><Link href="/tecnico/solicitudes?estado=Pendiente">Pendientes <strong>{resumen.datos.pendientes}</strong></Link></li>
        <li><Link href="/tecnico/solicitudes?estado=Aceptada">Aceptadas <strong>{resumen.datos.aceptadas}</strong></Link></li>
        <li><Link href="/tecnico/solicitudes?estado=EnProceso">En proceso <strong>{resumen.datos.enProceso}</strong></Link></li>
      </ul>
      <section aria-label="Próximas visitas"><h2>Próximas visitas</h2>
        {resumen.datos.proximasVisitas.length === 0 && <p>No tienes visitas futuras confirmadas.</p>}
        <ul className="directory-results">{resumen.datos.proximasVisitas.map(visita =>
          <li key={visita.id}><h3><Link href={'/solicitudes/' + visita.id}>{visita.titulo}</Link></h3>
            <p>{fechaVisible(visita.fechaConfirmada)}</p></li>)}</ul>
      </section>
    </>}
  </Marco>;
}
