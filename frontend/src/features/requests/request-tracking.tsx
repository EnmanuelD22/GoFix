'use client';

import Link from 'next/link';
import { useRef, useState, type FormEvent } from 'react';
import { AppShell } from '../../components/AppShell';
import { Campo } from '../../components/Campo';
import { Aviso, Cargando } from '../../components/Estados';
import { Fallo } from '../../components/Fallo';
import { Icon, type IconName } from '../../components/Icon';
import { StatCard } from '../../components/StatCard';
import { fechaVisible, plural, valorFechaLocal } from '../../lib/formato';
import { useConsulta } from '../../lib/use-consulta';
import { ErrorApi, guardar, type Cuenta } from '../auth/api';
import { EstadoTag, etiquetaEstado } from './estado';
import type { PaginaSolicitudes, Solicitud } from './request-views';

type Accion = 'aceptar' | 'rechazar' | 'cancelar' | 'iniciar' | 'completar';

const acciones: Record<Accion, { etiqueta: string; icono: IconName; clase: string; texto: string }> = {
  aceptar: { etiqueta: 'Aceptar solicitud', icono: 'check', clase: 'btn',
    texto: 'Confirma la fecha y hora de la visita. Al aceptar, el cliente y tú verán el teléfono de contacto.' },
  rechazar: { etiqueta: 'Rechazar solicitud', icono: 'close', clase: 'btn btn--danger-ghost',
    texto: 'Indica el motivo para que el cliente pueda buscar otra opción. Quedará en el historial.' },
  cancelar: { etiqueta: 'Cancelar solicitud', icono: 'close', clase: 'btn btn--danger-ghost',
    texto: 'Indica el motivo de la cancelación. Quedará registrado en el historial y la otra parte lo verá.' },
  iniciar: { etiqueta: 'Iniciar servicio', icono: 'play', clase: 'btn',
    texto: 'Se registrará la fecha y hora de inicio del servicio.' },
  completar: { etiqueta: 'Completar servicio', icono: 'check', clase: 'btn',
    texto: 'Registra una observación final si lo necesitas. Después, el cliente podrá valorar el servicio.' },
};

const tipos: Record<string, string> = { Creacion: 'Creación', Edicion: 'Edición', Transicion: 'Cambio de estado', Eliminacion: 'Eliminación' };

type Evento = {
  id: number; actorNombre: string; tipoEvento: string; estadoNuevo: string | null;
  createdAtUtc: string; observacion: string | null;
};
type Resumen = {
  pendientes: number; aceptadas: number; enProceso: number;
  proximasVisitas: { id: string; titulo: string; fechaConfirmada: string }[];
};

function accionesDisponibles(item: Solicitud, rol: Cuenta['rol']): Accion[] {
  const lista: Accion[] = [];
  if (rol === 'tecnico' && item.estado === 'Pendiente') lista.push('aceptar', 'rechazar');
  if (rol === 'tecnico' && item.estado === 'Aceptada') lista.push('iniciar');
  if (rol === 'tecnico' && item.estado === 'EnProceso') lista.push('completar');
  if ((rol === 'cliente' && item.estado === 'Pendiente') || item.estado === 'Aceptada') lista.push('cancelar');
  return lista;
}

/** Acciones, contacto e historial de una solicitud. `compacto` lo integra dentro de otra tarjeta. */
export function Seguimiento({ item, rol, recargar, compacto = false }: {
  item: Solicitud; rol: Cuenta['rol']; recargar: (accion?: Accion) => void; compacto?: boolean;
}) {
  const [accion, setAccion] = useState<Accion | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [ocupado, setOcupado] = useState(false);
  const [conflicto, setConflicto] = useState(false);
  const [largo, setLargo] = useState(0);
  const dialogo = useRef<HTMLDialogElement>(null);
  const enCurso = useRef(false);
  const historial = useConsulta<Evento[]>('/solicitudes/' + item.id + '/historial');
  const disponibles = accionesDisponibles(item, rol);
  const tarjeta = compacto ? 'tracking-block' : 'card tracking-block';
  const Titulo = compacto ? 'h4' : 'h2';

  function abrir(valor: Accion) {
    setAccion(valor);
    setError(null);
    setLargo(0);
    dialogo.current?.showModal();
  }

  async function confirmar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!accion || enCurso.current || conflicto) return;
    const form = new FormData(event.currentTarget);
    const fecha = String(form.get('fechaConfirmada') ?? '');
    if (accion === 'aceptar' && Number.isNaN(new Date(fecha).getTime())) return;
    const datos = {
      version: item.version,
      ...(accion === 'aceptar' ? { fechaConfirmada: new Date(fecha).toISOString() } : {}),
      ...(accion === 'cancelar' || accion === 'rechazar' ? { motivo: String(form.get('motivo')).trim() } : {}),
      ...(accion === 'completar' ? { observacion: String(form.get('observacion')).trim() } : {}),
    };
    enCurso.current = true;
    setOcupado(true);
    try {
      await guardar('/solicitudes/' + item.id + '/' + accion, 'POST', datos);
      dialogo.current?.close();
      recargar(accion);
    } catch (fallo) {
      setError(fallo);
      if (fallo instanceof ErrorApi && [0, 408, 404, 409].includes(fallo.status)) setConflicto(true);
    } finally {
      enCurso.current = false;
      setOcupado(false);
    }
  }

  const detalleAccion = accion ? acciones[accion] : null;
  return <>
    <section className={tarjeta} aria-labelledby={'acciones-' + item.id}>
      <Titulo id={'acciones-' + item.id}>Seguimiento</Titulo>
      {disponibles.length > 0
        ? <div className="button-row button-row--stack">{disponibles.map(valor => <button key={valor} type="button"
          className={acciones[valor].clase} onClick={() => abrir(valor)}><Icon name={acciones[valor].icono} size={18} />{acciones[valor].etiqueta}</button>)}</div>
        : <p className="muted">{item.estado === 'Completada' ? 'El servicio está completado.'
          : item.estado === 'Rechazada' || item.estado === 'Cancelada' ? 'Esta solicitud ya no admite cambios.'
          : rol === 'cliente' ? 'El técnico actualizará el estado del servicio. Te mostraremos cada cambio aquí.'
          : 'No hay acciones disponibles en este momento.'}</p>}
    </section>
    {item.contacto && <section className={tarjeta} aria-label="Contacto de la contraparte">
      <Titulo>{rol === 'tecnico' ? 'Contacto del cliente' : 'Contacto del técnico'}</Titulo>
      <p className="contact-line"><Icon name="user" size={18} />{item.contacto.nombre}</p>
      <p className="contact-line"><Icon name="phone" size={18} /><a href={'tel:' + item.contacto.telefono}>{item.contacto.telefono}</a></p>
    </section>}
    {!item.contacto && item.estado === 'Pendiente' && !compacto && <Aviso>
      <p>El teléfono de contacto se mostrará cuando el técnico acepte la solicitud.</p>
    </Aviso>}
    <section className={tarjeta + ' requests-history'} aria-label="Historial de la solicitud">
      <Titulo>Historial</Titulo>
      {historial.pendiente && <Cargando texto="Cargando historial…" />}
      {historial.error ? <Fallo error={historial.error} reintentar={historial.reintentar} enfocar={false} compacto /> : null}
      {historial.datos && (historial.datos.length === 0 ? <p className="muted">Sin eventos registrados.</p>
        : <ol className="timeline">{[...historial.datos].reverse().map(evento => <li key={evento.id}>
          <p className="timeline__title"><strong>{tipos[evento.tipoEvento] ?? evento.tipoEvento}</strong>
            {evento.estadoNuevo && <EstadoTag estado={evento.estadoNuevo} />}</p>
          <p className="timeline__meta">{evento.actorNombre} · <time dateTime={evento.createdAtUtc}>{fechaVisible(evento.createdAtUtc)}</time></p>
          {evento.observacion && <p className="timeline__note directory-description">{evento.observacion}</p>}
        </li>)}</ol>)}
    </section>
    <dialog ref={dialogo} className="dialog" aria-labelledby={'accion-' + item.id} aria-describedby={'accion-texto-' + item.id}
      onCancel={event => { if (ocupado) event.preventDefault(); }}>
      <h2 id={'accion-' + item.id}>{detalleAccion?.etiqueta ?? 'Confirmar acción'}</h2>
      <p id={'accion-texto-' + item.id} className="muted">{detalleAccion?.texto}</p>
      {error ? <Fallo error={error} /> : null}
      {conflicto && <p><button type="button" className="btn btn--ghost" onClick={() => { dialogo.current?.close(); recargar(); }}>
        <Icon name="refresh" size={18} />Cargar la versión actual</button></p>}
      <form className="dialog__form" key={accion} onSubmit={confirmar} aria-busy={ocupado}>
        <fieldset disabled={ocupado || conflicto}>
          {accion === 'aceptar' && <Campo etiqueta="Fecha y hora confirmadas" nombre="fechaConfirmada" error={error}
            ayuda={'Preferencia del cliente: ' + fechaVisible(item.fechaPreferida) + '.'}>
            {control => <input {...control} type="datetime-local" min={valorFechaLocal(new Date())}
              defaultValue={new Date(item.fechaPreferida) > new Date() ? valorFechaLocal(item.fechaPreferida) : undefined} required />}
          </Campo>}
          {(accion === 'rechazar' || accion === 'cancelar') && <Campo etiqueta="Motivo" nombre="motivo" error={error}
            ayuda="Entre 10 y 500 caracteres." contador={{ actual: largo, maximo: 500 }}>
            {control => <textarea {...control} minLength={10} maxLength={500} rows={4} required onChange={event => setLargo(event.target.value.length)} />}
          </Campo>}
          {accion === 'completar' && <Campo etiqueta="Observación final" nombre="observacion" error={error} opcional
            contador={{ actual: largo, maximo: 1000 }}>
            {control => <textarea {...control} maxLength={1000} rows={4} onChange={event => setLargo(event.target.value.length)} />}
          </Campo>}
          <div className="dialog__actions">
            <button type="button" className="btn btn--ghost" autoFocus onClick={() => dialogo.current?.close()}>Volver</button>
            <button type="submit" className={accion === 'rechazar' || accion === 'cancelar' ? 'btn btn--danger' : 'btn'}>{ocupado ? 'Guardando…' : 'Confirmar'}</button>
          </div>
        </fieldset>
      </form>
    </dialog>
  </>;
}

function Agenda() {
  const [seleccionada, setSeleccionada] = useState<string | null>(null);
  const pendientes = useConsulta<PaginaSolicitudes>('/solicitudes?estado=Pendiente&page=1&pageSize=10');
  const completadas = useConsulta<{ total: number }>('/solicitudes?estado=Completada&page=1&pageSize=1');
  const resumen = useConsulta<Resumen>('/tecnicos/me/resumen');
  const idActual = seleccionada ?? pendientes.datos?.items[0]?.id ?? resumen.datos?.proximasVisitas[0]?.id ?? null;
  const reintentarResumen = resumen.reintentar;
  const reintentarPendientes = pendientes.reintentar;
  const reintentarCompletadas = completadas.reintentar;
  function recargarPanel() { reintentarResumen(); reintentarPendientes(); reintentarCompletadas(); }

  if (resumen.pendiente) return <Cargando texto="Cargando agenda…" filas={3} />;
  if (resumen.error) return <Fallo error={resumen.error} reintentar={resumen.reintentar} />;
  const datos = resumen.datos!;
  return <>
    <section aria-label="Resumen de solicitudes"><dl className="stats">
      <StatCard icono="clock" tono="amber" etiqueta="Pendientes" valor={datos.pendientes} href="/tecnico/solicitudes?estado=Pendiente"
        descripcion={plural(datos.pendientes, 'solicitud pendiente', 'solicitudes pendientes')} />
      <StatCard icono="cal" tono="mint" etiqueta="Aceptadas" valor={datos.aceptadas} href="/tecnico/solicitudes?estado=Aceptada"
        descripcion={plural(datos.aceptadas, 'solicitud aceptada', 'solicitudes aceptadas')} />
      <StatCard icono="play" tono="indigo" etiqueta="En proceso" valor={datos.enProceso} href="/tecnico/solicitudes?estado=EnProceso"
        descripcion={plural(datos.enProceso, 'servicio en proceso', 'servicios en proceso')} />
      <StatCard icono="check" tono="violet" etiqueta="Completadas" valor={completadas.datos?.total} href="/tecnico/solicitudes?estado=Completada"
        descripcion={plural(completadas.datos?.total ?? 0, 'servicio completado', 'servicios completados')} />
    </dl>
    {completadas.error ? <Fallo error={completadas.error} reintentar={completadas.reintentar} enfocar={false} compacto /> : null}
    </section>
    <div className="panel-grid">
      <section className="card panel" aria-labelledby="visitas-title">
        <h2 id="visitas-title">Próximas visitas</h2>
        {datos.proximasVisitas.length === 0
          ? <div className="empty-inline"><Icon name="cal" /><p className="muted">No tienes visitas futuras confirmadas. Revisa tus solicitudes pendientes para organizar tu agenda.</p></div>
          : <div className="table-scroll"><table className="data-table">
            <caption className="muted">Tus próximas diez visitas confirmadas. Selecciona una para ver su detalle.</caption>
            <thead><tr><th scope="col">Servicio</th><th scope="col">Fecha</th><th scope="col">Estado</th></tr></thead>
            <tbody>{datos.proximasVisitas.map(visita => <tr key={visita.id} className={idActual === visita.id ? 'is-selected' : undefined}>
              <th scope="row"><button type="button" className="panel-select-link" onClick={() => setSeleccionada(visita.id)}
                aria-pressed={idActual === visita.id}>{visita.titulo}</button></th>
              <td><time dateTime={visita.fechaConfirmada}>{fechaVisible(visita.fechaConfirmada)}</time></td>
              <td><EstadoTag estado="Aceptada" /></td>
            </tr>)}</tbody>
          </table></div>}
      </section>
      <section className="card panel panel-request" aria-labelledby="panel-detalle-title">
        <h2 id="panel-detalle-title">Detalle de solicitud</h2>
        {pendientes.pendiente && <Cargando texto="Cargando solicitudes pendientes…" />}
        {pendientes.error ? <Fallo error={pendientes.error} reintentar={pendientes.reintentar} enfocar={false} /> : null}
        {pendientes.datos && pendientes.datos.items.length > 0 && <div className="field panel-request-picker">
          <label htmlFor="panel-solicitud">Solicitud pendiente</label>
          <select id="panel-solicitud" value={idActual ?? ''} onChange={event => setSeleccionada(event.target.value)}>
            {idActual && !pendientes.datos.items.some(item => item.id === idActual) && <option value={idActual}>Visita seleccionada en la agenda</option>}
            {pendientes.datos.items.map(item => <option key={item.id} value={item.id}>{item.titulo}</option>)}
          </select>
        </div>}
        {idActual ? <DetallePanel key={idActual} id={idActual} recargarPanel={recargarPanel} />
          : !pendientes.pendiente && !pendientes.error && <div className="empty-inline"><Icon name="inbox" />
            <p className="muted">No tienes solicitudes pendientes ni visitas próximas. Las nuevas solicitudes aparecerán aquí.</p></div>}
        <Link className="panel-all-requests" href="/tecnico/solicitudes">Todas las solicitudes asignadas<Icon name="arrow" size={18} /></Link>
      </section>
    </div>
  </>;
}

function DetallePanel({ id, recargarPanel }: { id: string; recargarPanel: () => void }) {
  const detalle = useConsulta<Solicitud>('/solicitudes/' + encodeURIComponent(id));
  const [mensaje, setMensaje] = useState<string | null>(null);
  function recargar(accion?: Accion) {
    setMensaje(accion ? 'Cambio guardado: ' + acciones[accion].etiqueta.toLowerCase() + '.' : null);
    detalle.reintentar();
    recargarPanel();
  }
  if (detalle.pendiente) return <Cargando texto="Cargando detalle…" />;
  if (detalle.error) return <Fallo error={detalle.error} reintentar={detalle.reintentar} enfocar={false} />;
  const item = detalle.datos!;
  return <div className="panel-request__body" aria-busy={detalle.recargando}>
    {mensaje && <div role="status"><Aviso tono="success"><p>{mensaje}</p></Aviso></div>}
    <div className="detail__head">
      <h3><Link href={'/solicitudes/' + id}>{item.titulo}</Link></h3>
      <EstadoTag estado={item.estado} />
    </div>
    <p className="muted directory-description panel-request__description">{item.descripcion}</p>
    <ul className="meta-list">
      <li><Icon name="cal" size={16} /><span>Fecha preferida: <time dateTime={item.fechaPreferida}>{fechaVisible(item.fechaPreferida)}</time></span></li>
      <li><Icon name="pin" size={16} />{item.direccion}</li>
      {item.fechaConfirmada && <li><Icon name="check" size={16} />Visita: {fechaVisible(item.fechaConfirmada)} ({etiquetaEstado(item.estado)})</li>}
    </ul>
    <Seguimiento key={item.version} item={item} rol="tecnico" recargar={recargar} compacto />
  </div>;
}

export function PanelTecnico() {
  return <AppShell rol="tecnico" titulo="Mi agenda de servicios" descripcion="Tus solicitudes y próximas visitas, en un solo lugar.">
    <Agenda />
  </AppShell>;
}
