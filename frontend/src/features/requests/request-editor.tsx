'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useId, useRef, useState, type FormEvent } from 'react';
import { AppShell, useCuenta } from '../../components/AppShell';
import { Campo } from '../../components/Campo';
import { Aviso, Cargando, Vacio } from '../../components/Estados';
import { Fallo } from '../../components/Fallo';
import { Icon } from '../../components/Icon';
import { iniciales, valorFechaLocal } from '../../lib/formato';
import { useConsulta } from '../../lib/use-consulta';
import { ErrorApi, guardar } from '../auth/api';
import { useCatalogos } from '../catalogs/catalogos';
import type { Cobertura, Solicitud } from './request-views';

const etiquetas: Record<string, string> = {
  especialidadId: 'Especialidad', sectorId: 'Sector', titulo: 'Título', descripcion: 'Descripción',
  direccion: 'Dirección', fechaPreferida: 'Fecha y hora preferidas',
};

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
  clienteId: string; cobertura: Cobertura; item?: Solicitud; actualizar?: () => void;
}) {
  const router = useRouter();
  const avisoId = useId();
  const [ocupado, setOcupado] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [conflicto, setConflicto] = useState(false);
  const [largos, setLargos] = useState({ titulo: item?.titulo.length ?? 0, descripcion: item?.descripcion.length ?? 0 });
  const enCurso = useRef(false);
  const intentos = useRef(new Map<string, string>());
  const fechaInicial = item ? valorFechaLocal(item.fechaPreferida) : '';
  const [minimo] = useState(() => valorFechaLocal(new Date()));
  const detalle = item ? '/solicitudes/' + item.id : '/cliente/solicitudes';
  const especialidades = cobertura.especialidades;
  const sectores = cobertura.sectores;

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
        const datos = { ...campos, tecnicoId: cobertura.id, especialidadId: Number(formulario.get('especialidadId')) };
        const borrador = await claveDeBorrador(clienteId, JSON.stringify(datos));
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
  const unicaEspecialidad = especialidades.length === 1 ? String(especialidades[0].id) : '';
  const unicoSector = sectores.length === 1 ? String(sectores[0].id) : '';
  return <>
    {error ? <Fallo error={error} id={avisoId} etiquetas={etiquetas} /> : null}
    {conflicto && <p><button type="button" className="btn btn--ghost" onClick={actualizar}><Icon name="refresh" size={18} />Cargar la versión actual</button></p>}
    <form onSubmit={enviar} className="card requests-form stack-form" aria-busy={ocupado} aria-describedby={error ? avisoId : undefined}>
      <fieldset disabled={ocupado || conflicto}>
        <legend className="sr-only">Datos de la visita</legend>
        <div className="form-grid">
          {item
            ? especialidad && <div className="field"><span className="field__label">Especialidad</span><p className="field__static">{especialidad.nombre}</p></div>
            : <Campo etiqueta="Especialidad" nombre="especialidadId" error={error}>
              {control => <select {...control} defaultValue={unicaEspecialidad} required>
                <option value="">Elige una especialidad</option>
                {especialidades.map(opcion => <option key={opcion.id} value={opcion.id}>{opcion.nombre}</option>)}
              </select>}
            </Campo>}
          <Campo etiqueta="Sector" nombre="sectorId" error={error} ayuda="Solo se muestran los sectores que atiende el técnico.">
            {control => <select {...control} defaultValue={item?.sectorId ?? unicoSector} required>
              <option value="">Elige un sector</option>
              {sectores.map(opcion => <option key={opcion.id} value={opcion.id}>{opcion.nombre}</option>)}
            </select>}
          </Campo>
        </div>
        <Campo etiqueta="Título" nombre="titulo" error={error} ayuda="Resume el problema en una frase. Ejemplo: Reparación de breaker en la cocina."
          contador={{ actual: largos.titulo, maximo: 120 }}>
          {control => <input {...control} minLength={10} maxLength={120} defaultValue={item?.titulo} required
            onChange={event => setLargos(actual => ({ ...actual, titulo: event.target.value.length }))} />}
        </Campo>
        <Campo etiqueta="Descripción" nombre="descripcion" error={error} ayuda="Explica qué ocurre, desde cuándo y cualquier detalle útil para la visita."
          contador={{ actual: largos.descripcion, maximo: 2000 }}>
          {control => <textarea {...control} minLength={20} maxLength={2000} rows={5} defaultValue={item?.descripcion} required
            onChange={event => setLargos(actual => ({ ...actual, descripcion: event.target.value.length }))} />}
        </Campo>
        <div className="form-grid">
          <Campo etiqueta="Dirección" nombre="direccion" error={error} ayuda="Calle, número y una referencia. Mínimo 10 caracteres.">
            {control => <input {...control} minLength={10} maxLength={300} autoComplete="street-address" defaultValue={item?.direccion} required />}
          </Campo>
          <Campo etiqueta="Fecha y hora preferidas" nombre="fechaPreferida" error={error} ayuda="El técnico confirmará la fecha definitiva al aceptar.">
            {control => <input {...control} type="datetime-local" min={item ? undefined : minimo} defaultValue={fechaInicial} required />}
          </Campo>
        </div>
        <div className="button-row">
          <button className="btn" type="submit">{ocupado ? <><span className="spinner" aria-hidden="true" />Guardando…</>
            : item ? 'Guardar cambios' : 'Enviar solicitud'}</button>
          <Link className="btn btn--ghost" href={detalle}>Cancelar</Link>
        </div>
      </fieldset>
    </form>
  </>;
}

function ResumenTecnico({ cobertura }: { cobertura: Cobertura }) {
  if (!cobertura.nombre) return null;
  return <aside className="card tech-summary" aria-label="Técnico seleccionado">
    <span className="tech-card__avatar" aria-hidden="true">{iniciales(cobertura.nombre)}</span>
    <div>
      <p className="muted">Solicitud para</p>
      <p className="tech-summary__name"><Link href={'/tecnicos/' + cobertura.id}>{cobertura.nombre}</Link></p>
      <p className="muted">{cobertura.especialidades.map(item => item.nombre).join(' · ')}</p>
    </div>
  </aside>;
}

function Contenido({ id, tecnico }: { id?: string; tecnico?: string }) {
  const { cuenta } = useCuenta();
  const detalle = useConsulta<Solicitud>(id ? '/solicitudes/' + encodeURIComponent(id) : null);
  const tecnicoId = detalle.datos?.tecnicoId ?? (id ? undefined : tecnico);
  const perfil = useConsulta<Cobertura>(tecnicoId ? '/tecnicos/' + encodeURIComponent(tecnicoId) : null);
  // Si el perfil dejó de publicarse, la edición usa los catálogos activos.
  const sinPerfil = Boolean(detalle.datos) && perfil.error instanceof ErrorApi && perfil.error.status === 404;
  const catalogos = useCatalogos();
  const cobertura: Cobertura | undefined = perfil.datos ?? (sinPerfil && catalogos.datos && tecnicoId
    ? { id: tecnicoId, nombre: '', sectores: catalogos.datos.sectores, especialidades: catalogos.datos.especialidades } : undefined);

  if (!id && !tecnico) return <Vacio icono="search" titulo="Elige primero un técnico" accion={{ href: '/tecnicos', texto: 'Buscar técnicos' }}>
    Busca un técnico de tu zona y elige «Solicitar servicio» en su perfil.
  </Vacio>;
  if (detalle.pendiente || perfil.pendiente || (sinPerfil && catalogos.pendiente)) return <Cargando texto="Cargando datos de la visita…" filas={4} />;
  if (detalle.error) return <Fallo error={detalle.error} reintentar={detalle.reintentar} />;
  if (perfil.error && !sinPerfil) return <>
    <Fallo error={perfil.error} reintentar={perfil.reintentar}
      mensajes={{ 404: 'Este técnico ya no está disponible. Elige otro técnico en el directorio.' }} />
    <p><Link href="/tecnicos">Volver al directorio</Link></p>
  </>;
  if (sinPerfil && catalogos.error) return <Fallo error={catalogos.error} reintentar={catalogos.reintentar} />;
  if (detalle.datos && detalle.datos.estado !== 'Pendiente') return <Aviso tono="warning">
    <p>Solo puedes editar solicitudes pendientes. <Link href={'/solicitudes/' + detalle.datos.id}>Volver al detalle</Link></p>
  </Aviso>;
  if (!cobertura) return null;
  return <div className="editor-layout">
    <ResumenTecnico cobertura={cobertura} />
    {sinPerfil && <Aviso tono="warning"><p>El perfil del técnico ya no está publicado. Puedes actualizar los datos de la visita con los catálogos activos.</p></Aviso>}
    <Formulario key={detalle.datos?.version ?? cobertura.id} clienteId={cuenta.id} cobertura={cobertura}
      item={detalle.datos} actualizar={detalle.reintentar} />
  </div>;
}

export function EditorSolicitud({ id, tecnico }: { id?: string; tecnico?: string }) {
  return <AppShell rol="cliente" titulo={id ? 'Editar solicitud' : 'Nueva solicitud'}
    descripcion={id ? 'Puedes modificar la solicitud mientras siga pendiente.' : 'Describe el servicio que necesitas. El técnico la revisará y confirmará la visita.'}
    volver={id ? { href: '/solicitudes/' + id, texto: 'Volver al detalle' } : { href: '/cliente/solicitudes', texto: 'Mis solicitudes' }}>
    <Contenido id={id} tecnico={tecnico} />
  </AppShell>;
}
