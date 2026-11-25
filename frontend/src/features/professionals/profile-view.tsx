'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { consultarCuenta, ErrorApi, guardar, solicitar } from '../auth/api';

type Especialidad = { id: number; nombre: string };
type Sector = Especialidad & { municipio: string };
type Perfil = {
  id: string;
  descripcion: string;
  especialidadIds: number[];
  sectorIds: number[];
  version: string;
  publicado: boolean;
};

function alternar(ids: number[], id: number) {
  return ids.includes(id) ? ids.filter(valor => valor !== id) : [...ids, id];
}

export function PerfilProfesional() {
  const router = useRouter();
  const [perfil, setPerfil] = useState<Perfil | null>(null);
  const [especialidades, setEspecialidades] = useState<Especialidad[]>([]);
  const [sectores, setSectores] = useState<Sector[]>([]);
  const [descripcion, setDescripcion] = useState('');
  const [especialidadIds, setEspecialidadIds] = useState<number[]>([]);
  const [sectorIds, setSectorIds] = useState<number[]>([]);
  const [cargando, setCargando] = useState(true);
  const [ocupado, setOcupado] = useState(false);
  const enCurso = useRef(false);
  const aviso = useRef<HTMLDivElement>(null);
  const [error, setError] = useState<unknown>(null);
  useEffect(() => { if (error) aviso.current?.focus(); }, [error]);
  const [guardado, setGuardado] = useState(false);
  const [conflicto, setConflicto] = useState(false);
  const [comparacion, setComparacion] = useState<Perfil | null>(null);
  const [intento, setIntento] = useState(0);

  function mostrarPerfil(actual: Perfil) {
    setPerfil(actual);
    setDescripcion(actual.descripcion);
    setEspecialidadIds(actual.especialidadIds);
    setSectorIds(actual.sectorIds);
  }

  useEffect(() => {
    const control = new AbortController();
    async function cargar() {
      try {
        const cuenta = await consultarCuenta(control.signal);
        if (control.signal.aborted) return;
        if (cuenta.rol !== 'tecnico') {
          router.replace('/cuenta');
          return;
        }
        const [actual, listaEspecialidades, listaSectores] = await Promise.all([
          solicitar<Perfil>('/tecnicos/me/perfil', { signal: control.signal }),
          solicitar<Especialidad[]>('/especialidades', { signal: control.signal }),
          solicitar<Sector[]>('/sectores', { signal: control.signal }),
        ]);
        if (control.signal.aborted) return;
        mostrarPerfil(actual);
        setEspecialidades(listaEspecialidades);
        setSectores(listaSectores);
      } catch (fallo) {
        if (control.signal.aborted) return;
        if (fallo instanceof ErrorApi && fallo.status === 401) router.replace('/acceso?sesion=expirada');
        else setError(fallo);
      } finally {
        if (!control.signal.aborted) setCargando(false);
      }
    }
    void cargar();
    return () => control.abort();
  }, [router, intento]);

  async function enviar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (enCurso.current || !perfil || conflicto) return;
    enCurso.current = true;
    setOcupado(true);
    setGuardado(false);
    setError(null);
    try {
      const actual = await guardar<Perfil>('/tecnicos/me/perfil', 'PUT', {
        descripcion, especialidadIds, sectorIds, version: perfil.version,
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

  const api = error instanceof ErrorApi ? error : null;
  const errores = api?.problema.errors ?? {};
  const faltanCatalogos = especialidades.length === 0 || sectores.length === 0;
  const seleccionesRetiradas = especialidadIds.filter(id => !especialidades.some(item => item.id === id)).length
    + sectorIds.filter(id => !sectores.some(item => item.id === id)).length;
  const mensaje = api?.status === 409 ? 'El perfil cambió desde que lo abriste. Conservamos tu edición. Consulta la versión actual antes de volver a guardar.'
    : api?.status === 401 ? 'Tu sesión venció. Inicia sesión de nuevo.'
    : api?.status === 400 ? 'Revisa los datos del perfil.'
    : api && [0, 408].includes(api.status) ? api.message
    : 'No se pudo completar la operación. Vuelve a intentarlo.';

  return (
    <main className="identity professional" aria-labelledby="profile-title">
      <div className="identity-panel">
        <Link href="/" className="brand identity-brand">GoFix</Link>
        <h1 id="profile-title">Mi perfil profesional</h1>
        <p><Link href="/cuenta">Volver a mi cuenta</Link></p>
        {cargando && <p role="status">Cargando tu perfil…</p>}
        {Boolean(error) && <div ref={aviso} tabIndex={-1} className="identity-notice" role="alert">
          <p>{mensaje}</p>
          {Object.entries(errores).length > 0 && <ul>{Object.entries(errores).map(([campo, mensajes]) => (
            <li id={`perfil-error-${campo}`} key={campo}>{mensajes.join(' ')}</li>
          ))}</ul>}
          {api?.status === 401 && <Link href="/acceso">Iniciar sesión</Link>}
          {api?.problema.traceId && <small>Referencia {api.problema.traceId}</small>}
        </div>}
        {!perfil && !cargando && Boolean(error) && <button type="button" onClick={() => {
          setError(null); setCargando(true); setIntento(intento + 1);
        }}>Volver a intentar</button>}
        {perfil && <>
          <p className="profile-publication">{perfil.publicado ? 'Perfil publicado' : 'Perfil pendiente de completar'}</p>
          <p>Para publicar tu perfil, guarda una descripción de 50 a 1.000 caracteres y selecciona al menos una especialidad y un sector activos.</p>
          {faltanCatalogos && <p className="identity-notice" role="status">Los catálogos necesarios todavía no están disponibles. Vuelve a consultar cuando se hayan habilitado especialidades y sectores.</p>}
          {seleccionesRetiradas > 0 && <div className="identity-notice">
            <p>Hay selecciones anteriores que ya no están disponibles. Revisa la cobertura activa antes de guardar.</p>
            <button type="button" disabled={ocupado} onClick={() => {
              setEspecialidadIds(especialidadIds.filter(id => especialidades.some(item => item.id === id)));
              setSectorIds(sectorIds.filter(id => sectores.some(item => item.id === id)));
            }}>Quitar selecciones retiradas</button>
          </div>}
          {conflicto && <button type="button" disabled={ocupado} onClick={consultarVersion}>Consultar versión actual</button>}
          {comparacion && <section className="profile-comparison" aria-label="Perfil guardado">
            <h2>Versión guardada</h2>
            <p>{comparacion.descripcion || 'Sin descripción.'}</p>
            <p>Especialidades: {especialidades.filter(item => comparacion.especialidadIds.includes(item.id)).map(item => item.nombre).join(', ') || 'Sin selección activa'}.</p>
            <p>Sectores: {sectores.filter(item => comparacion.sectorIds.includes(item.id)).map(item => item.nombre).join(', ') || 'Sin selección activa'}.</p>
            <p>Tu formulario conserva la edición anterior. Revísala y guarda para confirmar tus cambios sobre esta versión.</p>
          </section>}
          {guardado && <p role="status" className="identity-success">Perfil guardado.</p>}
          <form method="post" onSubmit={enviar} aria-busy={ocupado}>
            <fieldset disabled={ocupado}>
              <label>Descripción de tus servicios
                <textarea name="descripcion" rows={6} minLength={50} maxLength={1000} required value={descripcion} onChange={event => setDescripcion(event.target.value)}
                  aria-invalid={Boolean(errores.descripcion) || undefined} aria-describedby={errores.descripcion ? 'perfil-error-descripcion' : undefined} />
              </label>
              <fieldset className="profile-options" aria-describedby={errores.especialidadIds ? 'perfil-error-especialidadIds' : undefined}>
                <legend>Especialidades</legend>
                {especialidades.map(item => <label key={item.id}>
                  <input type="checkbox" checked={especialidadIds.includes(item.id)} onChange={() => setEspecialidadIds(alternar(especialidadIds, item.id))} />{item.nombre}
                </label>)}
              </fieldset>
              <fieldset className="profile-options" aria-describedby={errores.sectorIds ? 'perfil-error-sectorIds' : undefined}>
                <legend>Sectores que atiendes</legend>
                {sectores.map(item => <label key={item.id}>
                  <input type="checkbox" checked={sectorIds.includes(item.id)} onChange={() => setSectorIds(alternar(sectorIds, item.id))} />{item.nombre} ({item.municipio})
                </label>)}
              </fieldset>
              <button className="identity-button" disabled={conflicto || faltanCatalogos}>{ocupado ? 'Guardando…' : 'Guardar perfil'}</button>
            </fieldset>
          </form>
        </>}
      </div>
    </main>
  );
}
