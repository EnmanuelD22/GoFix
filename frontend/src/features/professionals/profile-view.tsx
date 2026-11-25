'use client';

import Link from 'next/link';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { AppShell } from '../../components/AppShell';
import { Campo } from '../../components/Campo';
import { Aviso, Cargando } from '../../components/Estados';
import { erroresDeCampo, Fallo } from '../../components/Fallo';
import { Icon } from '../../components/Icon';
import { plural } from '../../lib/formato';
import { useConsulta } from '../../lib/use-consulta';
import { ErrorApi, guardar, solicitar } from '../auth/api';
import { useCatalogos, type Especialidad } from '../catalogs/catalogos';

type Perfil = {
  id: string;
  descripcion: string;
  especialidadIds: number[];
  sectorIds: number[];
  version: string;
  publicado: boolean;
};

const MIN_DESCRIPCION = 50;
const MAX_DESCRIPCION = 1000;
const mensajesPerfil = {
  409: 'El perfil cambió desde que lo abriste. Conservamos tu edición. Consulta la versión actual antes de volver a guardar.',
  400: 'Revisa los datos del perfil.',
};

function alternar(ids: number[], id: number) {
  return ids.includes(id) ? ids.filter(valor => valor !== id) : [...ids, id];
}

function Opciones<T extends Especialidad>({ leyenda, opciones, seleccion, cambiar, error, etiqueta }: {
  leyenda: string; opciones: T[]; seleccion: number[]; cambiar: (ids: number[]) => void;
  error?: string[]; etiqueta: (item: T) => string;
}) {
  const errorId = 'error-' + leyenda.replace(/\s+/g, '-').toLowerCase();
  return <fieldset className="check-group" aria-describedby={error ? errorId : undefined} aria-invalid={error ? true : undefined}>
    <legend>{leyenda} <span className="muted">({plural(seleccion.filter(id => opciones.some(item => item.id === id)).length, 'seleccionada', 'seleccionadas')})</span></legend>
    <div className="check-group__options">
      {opciones.map(item => <label key={item.id} className="check-chip">
        <input type="checkbox" checked={seleccion.includes(item.id)} onChange={() => cambiar(alternar(seleccion, item.id))} />
        <span><Icon name="check" size={16} className="check-chip__mark" />{etiqueta(item)}</span>
      </label>)}
    </div>
    {error && <p id={errorId} className="field__error"><Icon name="alert" size={16} />{error.join(' ')}</p>}
  </fieldset>;
}

function EditorPerfil() {
  const catalogos = useCatalogos();
  const consulta = useConsulta<Perfil>('/tecnicos/me/perfil');
  const [perfil, setPerfil] = useState<Perfil | null>(null);
  const [descripcion, setDescripcion] = useState('');
  const [especialidadIds, setEspecialidadIds] = useState<number[]>([]);
  const [sectorIds, setSectorIds] = useState<number[]>([]);
  const [ocupado, setOcupado] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [guardado, setGuardado] = useState(false);
  const [conflicto, setConflicto] = useState(false);
  const [comparacion, setComparacion] = useState<Perfil | null>(null);
  const enCurso = useRef(false);

  function mostrarPerfil(actual: Perfil) {
    setPerfil(actual);
    setDescripcion(actual.descripcion);
    setEspecialidadIds(actual.especialidadIds);
    setSectorIds(actual.sectorIds);
  }

  useEffect(() => {
    if (consulta.datos && !perfil) mostrarPerfil(consulta.datos);
  }, [consulta.datos, perfil]);

  async function enviar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (enCurso.current || !perfil || conflicto) return;
    enCurso.current = true;
    setOcupado(true);
    setGuardado(false);
    setError(null);
    try {
      const actual = await guardar<Perfil>('/tecnicos/me/perfil', 'PUT', {
        descripcion: descripcion.trim(), especialidadIds, sectorIds, version: perfil.version,
      });
      mostrarPerfil(actual);
      setComparacion(null);
      setGuardado(true);
    } catch (fallo) {
      setError(fallo);
      if (fallo instanceof ErrorApi && fallo.status === 409) setConflicto(true);
    } finally { enCurso.current = false; setOcupado(false); }
  }

  async function consultarVersion() {
    if (enCurso.current) return;
    enCurso.current = true;
    setOcupado(true);
    setError(null);
    try {
      const actual = await solicitar<Perfil>('/tecnicos/me/perfil');
      // La edición local se conserva mientras la persona compara y decide volver a guardar.
      setPerfil(actual);
      setComparacion(actual);
      setConflicto(false);
    } catch (fallo) { setError(fallo); }
    finally { enCurso.current = false; setOcupado(false); }
  }

  if (consulta.pendiente || catalogos.pendiente) return <Cargando texto="Cargando tu perfil…" filas={4} />;
  if (consulta.error) return <Fallo error={consulta.error} reintentar={consulta.reintentar} />;
  if (catalogos.error) return <Fallo error={catalogos.error} reintentar={catalogos.reintentar} />;
  if (!perfil) return null;

  const especialidades = catalogos.datos?.especialidades ?? [];
  const sectores = catalogos.datos?.sectores ?? [];
  const errores = erroresDeCampo(error);
  const faltanCatalogos = especialidades.length === 0 || sectores.length === 0;
  const seleccionesRetiradas = especialidadIds.filter(id => !especialidades.some(item => item.id === id)).length
    + sectorIds.filter(id => !sectores.some(item => item.id === id)).length;
  const largo = descripcion.trim().length;
  const requisitos = [
    { texto: `Descripción de ${MIN_DESCRIPCION} a ${MAX_DESCRIPCION.toLocaleString('es-DO')} caracteres`, ok: largo >= MIN_DESCRIPCION && largo <= MAX_DESCRIPCION },
    { texto: 'Al menos una especialidad activa', ok: especialidadIds.some(id => especialidades.some(item => item.id === id)) },
    { texto: 'Al menos un sector activo', ok: sectorIds.some(id => sectores.some(item => item.id === id)) },
  ];

  return <div className="profile-editor">
    <div className="profile-editor__main">
      {guardado && <div role="status"><Aviso tono="success"><p>Perfil guardado.{perfil.publicado ? ' Ya aparece en el directorio público.' : ''}</p></Aviso></div>}
      {error ? <Fallo error={error} mensajes={mensajesPerfil} /> : null}
      {conflicto && <p><button type="button" className="btn btn--ghost" disabled={ocupado} onClick={consultarVersion}><Icon name="refresh" size={18} />Consultar versión actual</button></p>}
      {faltanCatalogos && <Aviso tono="warning"><p>Los catálogos necesarios todavía no están disponibles. Vuelve a consultar cuando se hayan habilitado especialidades y sectores.</p></Aviso>}
      {seleccionesRetiradas > 0 && <Aviso tono="warning">
        <p>Hay selecciones anteriores que ya no están disponibles. Revisa la cobertura activa antes de guardar.</p>
        <button type="button" className="btn btn--sm btn--light" disabled={ocupado} onClick={() => {
          setEspecialidadIds(especialidadIds.filter(id => especialidades.some(item => item.id === id)));
          setSectorIds(sectorIds.filter(id => sectores.some(item => item.id === id)));
        }}>Quitar selecciones retiradas</button>
      </Aviso>}
      {comparacion && <section className="notice notice--info profile-comparison" aria-labelledby="comparacion-title">
        <div className="notice__body">
          <h2 id="comparacion-title">Versión guardada</h2>
          <p className="directory-description">{comparacion.descripcion || 'Sin descripción.'}</p>
          <p>Especialidades: {especialidades.filter(item => comparacion.especialidadIds.includes(item.id)).map(item => item.nombre).join(', ') || 'Sin selección activa'}.</p>
          <p>Sectores: {sectores.filter(item => comparacion.sectorIds.includes(item.id)).map(item => item.nombre).join(', ') || 'Sin selección activa'}.</p>
          <p>Tu formulario conserva la edición anterior. Revísala y guarda para confirmar tus cambios sobre esta versión.</p>
        </div>
      </section>}
      <form method="post" onSubmit={enviar} aria-busy={ocupado} className="card stack-form">
        <fieldset disabled={ocupado}>
          <Campo etiqueta="Descripción de tus servicios" nombre="descripcion" error={error}
            ayuda="Cuenta qué trabajos realizas, tu experiencia y cómo organizas las visitas."
            contador={{ actual: descripcion.length, maximo: MAX_DESCRIPCION }}>
            {control => <textarea {...control} rows={7} minLength={MIN_DESCRIPCION} maxLength={MAX_DESCRIPCION} required value={descripcion}
              onChange={event => setDescripcion(event.target.value)} />}
          </Campo>
          <Opciones leyenda="Especialidades" opciones={especialidades} seleccion={especialidadIds} cambiar={setEspecialidadIds}
            error={errores.especialidadIds} etiqueta={item => item.nombre} />
          <Opciones leyenda="Sectores que atiendes" opciones={sectores} seleccion={sectorIds} cambiar={setSectorIds}
            error={errores.sectorIds} etiqueta={item => `${item.nombre} (${item.municipio})`} />
          <button className="btn" type="submit" disabled={conflicto || faltanCatalogos}>{ocupado ? 'Guardando…' : 'Guardar perfil'}</button>
        </fieldset>
      </form>
    </div>
    <aside className="profile-editor__aside" aria-labelledby="estado-perfil-title">
      <div className="card">
        <h2 id="estado-perfil-title">Estado del perfil</h2>
        <p className={'tag ' + (perfil.publicado ? 'tag--success' : 'tag--warning')}>{perfil.publicado ? 'Perfil publicado' : 'Pendiente de completar'}</p>
        <p className="muted section-lead">Tu perfil aparece en el directorio cuando cumple estos requisitos y lo guardas:</p>
        <ul className="requirements">{requisitos.map(item => <li key={item.texto} className={item.ok ? 'is-ok' : undefined}>
          <Icon name={item.ok ? 'check' : 'info'} size={18} />{item.texto}<span className="sr-only">{item.ok ? ': cumplido' : ': pendiente'}</span>
        </li>)}</ul>
        {perfil.publicado && <Link className="btn btn--ghost btn--block" href={'/tecnicos/' + perfil.id}><Icon name="eye" size={18} />Ver mi perfil público</Link>}
      </div>
    </aside>
  </div>;
}

export function PerfilProfesional() {
  return <AppShell rol="tecnico" titulo="Mi perfil profesional"
    descripcion="Describe tu trabajo y elige dónde lo ofreces para recibir solicitudes de tu zona.">
    <EditorPerfil />
  </AppShell>;
}
