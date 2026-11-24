'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useRef, useState, type FormEvent } from 'react';
import { ErrorApi, guardar, type Cuenta } from '../auth/api';
import { Fallo, Marco, useConsulta, type Cobertura, type Opcion, type Solicitud } from './request-views';

function fechaLocal(fecha: string) {
  const local = new Date(fecha);
  return new Date(local.getTime() - local.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}

async function claveDeBorrador(clienteId: string, contenido: string) {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(contenido));
  const huella = Array.from(new Uint8Array(digest), octeto => octeto.toString(16).padStart(2, '0')).join('');
  return 'gofix:solicitud:' + clienteId + ':' + huella;
}

function leerClave(borrador: string) {
  try { return sessionStorage.getItem(borrador); }
  catch { return null; }
}

function guardarClave(borrador: string, clave: string | null) {
  try {
    if (clave) sessionStorage.setItem(borrador, clave);
    else sessionStorage.removeItem(borrador);
  } catch {
    // La clave en memoria permite reintentar si el navegador bloquea el almacenamiento.
  }
}

function Formulario({ clienteId, cobertura, item, actualizar }: {
  clienteId: string; cobertura?: Cobertura; item?: Solicitud; actualizar?: () => void;
}) {
  const router = useRouter();
  const [ocupado, setOcupado] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [conflicto, setConflicto] = useState(false);
  const enCurso = useRef(false);
  const intentos = useRef(new Map<string, string>());
  const fechaInicial = item ? fechaLocal(item.fechaPreferida) : '';
  const id = item?.id;
  const detalle = id ? '/solicitudes/' + id : '/cliente/solicitudes';
  const tecnicoId = item?.tecnicoId ?? cobertura?.id;
  const especialidades = cobertura?.especialidades ?? [];
  const sectores = cobertura?.sectores ?? [];

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    if (enCurso.current || conflicto) return;
    const formulario = new FormData(evento.currentTarget);
    const fecha = String(formulario.get('fechaPreferida'));
    const fechaElegida = new Date(fecha);
    if (Number.isNaN(fechaElegida.getTime())) {
      setError(new ErrorApi(400, { errors: { fechaPreferida: ['Elige una fecha y hora válidas.'] } }));
      return;
    }
    const campos = {
      titulo: String(formulario.get('titulo')).trim(),
      descripcion: String(formulario.get('descripcion')).trim(),
      direccion: String(formulario.get('direccion')).trim(),
      sectorId: Number(formulario.get('sectorId')),
      fechaPreferida: item && fecha === fechaInicial ? item.fechaPreferida : fechaElegida.toISOString(),
    };

    enCurso.current = true;
    setOcupado(true);
    setError(null);
    let guardado = false;
    try {
      if (item) {
        await guardar<Solicitud>(detalle, 'PUT', { ...campos, version: item.version });
        router.replace(detalle);
      } else {
        const datos = { ...campos, tecnicoId, especialidadId: Number(formulario.get('especialidadId')) };
        const contenido = JSON.stringify(datos);
        const borrador = await claveDeBorrador(clienteId, contenido);
        const clave = intentos.current.get(borrador) ?? leerClave(borrador) ?? crypto.randomUUID();
        intentos.current.set(borrador, clave);
        guardarClave(borrador, clave);
        const nueva = await guardar<Solicitud>('/solicitudes', 'POST', datos, { 'Idempotency-Key': clave });
        guardarClave(borrador, null);
        router.replace('/solicitudes/' + nueva.id);
      }
      guardado = true;
    } catch (fallo) {
      setError(fallo);
      if (item && fallo instanceof ErrorApi && [404, 409].includes(fallo.status)) setConflicto(true);
    } finally {
      if (!guardado) {
        enCurso.current = false;
        setOcupado(false);
      }
    }
  }

  const especialidad = especialidades.find(opcion => opcion.id === item?.especialidadId);
  return <>
    {error ? <Fallo error={error} /> : null}
    {conflicto && <p><button type="button" onClick={actualizar}>Cargar la versión actual</button></p>}
    <form onSubmit={enviar} className="requests-form">
      <fieldset disabled={ocupado || conflicto}>
        <legend>{cobertura?.nombre ? 'Visita con ' + cobertura.nombre : 'Datos de la visita'}</legend>
        {item && especialidad && <p>Especialidad: {especialidad.nombre}</p>}
        {!item && <label>Especialidad<select name="especialidadId" defaultValue="" required>
          <option value="">Elige una especialidad</option>
          {especialidades.map(opcion => <option key={opcion.id} value={opcion.id}>{opcion.nombre}</option>)}
        </select></label>}
        <label>Sector<select name="sectorId" defaultValue={item?.sectorId ?? ''} required>
          <option value="">Elige un sector</option>
          {sectores.map(opcion => <option key={opcion.id} value={opcion.id}>{opcion.nombre}</option>)}
        </select></label>
        <label>Título<input name="titulo" defaultValue={item?.titulo} maxLength={120} required /></label>
        <label>Descripción<textarea name="descripcion" defaultValue={item?.descripcion} maxLength={2000} rows={5} required /></label>
        <label>Dirección<input name="direccion" defaultValue={item?.direccion} maxLength={300} required /></label>
        <label>Fecha y hora preferidas<input type="datetime-local" name="fechaPreferida" defaultValue={fechaInicial} required /></label>
        <button className="identity-button" type="submit">{ocupado ? 'Guardando…' : item ? 'Guardar cambios' : 'Enviar solicitud'}</button>
      </fieldset>
    </form>
  </>;
}

export function EditorSolicitud({ id, tecnico }: { id?: string; tecnico?: string }) {
  const cuenta = useConsulta<Cuenta>('/auth/me');
  const cliente = cuenta.datos?.rol === 'cliente' ? cuenta.datos : undefined;
  const detalle = useConsulta<Solicitud>(cliente && id ? '/solicitudes/' + encodeURIComponent(id) : null);
  const tecnicoId = detalle.datos?.tecnicoId ?? tecnico;
  const perfil = useConsulta<Cobertura>(cliente && tecnicoId ? '/tecnicos/' + encodeURIComponent(tecnicoId) : null);
  const sectores = useConsulta<Opcion[]>(detalle.datos && perfil.error instanceof ErrorApi && perfil.error.status === 404 ? '/sectores' : null);
  const especialidades = useConsulta<Opcion[]>(sectores.datos ? '/especialidades' : null);
  const cobertura = perfil.datos ?? (sectores.datos && especialidades.datos && tecnicoId
    ? { id: tecnicoId, nombre: '', sectores: sectores.datos, especialidades: especialidades.datos } : undefined);
  const enlace = id ? '/solicitudes/' + id : '/cliente/solicitudes';
  const error = cuenta.error ?? detalle.error ?? sectores.error ?? especialidades.error ??
    (sectores.pendiente || sectores.datos ? undefined : perfil.error);

  return <Marco titulo={id ? 'Editar solicitud' : 'Nueva solicitud'}>
    <p><Link href={enlace}>{id ? 'Volver al detalle' : 'Volver a mis solicitudes'}</Link></p>
    {(cuenta.pendiente || detalle.pendiente || perfil.pendiente || sectores.pendiente || especialidades.pendiente) &&
      <p role="status">Cargando datos de la visita…</p>}
    {error ? <Fallo error={error} reintentar={cuenta.error ? cuenta.reintentar : detalle.error ? detalle.reintentar
      : sectores.error ? sectores.reintentar : especialidades.error ? especialidades.reintentar : perfil.reintentar} /> : null}
    {cuenta.datos?.rol === 'tecnico' && <p>Las solicitudes se crean y editan desde una cuenta de cliente.</p>}
    {cliente && !id && !tecnico && <p><Link href="/tecnicos">Busca un técnico</Link> y elige «Solicitar servicio» en su perfil.</p>}
    {detalle.datos && detalle.datos.estado !== 'Pendiente' && <p>Solo puedes editar solicitudes pendientes.</p>}
    {cliente && cobertura && (!id || detalle.datos?.estado === 'Pendiente') &&
      <Formulario key={detalle.datos?.version ?? cobertura.id} clienteId={cliente.id} cobertura={cobertura}
        item={detalle.datos} actualizar={detalle.reintentar} />}
  </Marco>;
}
