'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { ErrorApi, solicitar } from '../auth/api';

type Especialidad = { id: number; nombre: string };
type Sector = Especialidad & { municipio: string };
type Tecnico = {
  id: string;
  nombre: string;
  descripcion: string;
  especialidades: Especialidad[];
  sectores: Sector[];
  promedio: number | null;
  cantidadValoraciones: number;
};
type Resultados = { items: Tecnico[]; page: number; pageSize: number; total: number };

function Marco({ titulo, children }: { titulo: string; children: ReactNode }) {
  return <main className="directory" aria-labelledby="directory-title">
    <div className="directory-content">
      <nav className="directory-nav" aria-label="Navegación principal">
        <Link className="brand identity-brand" href="/">GoFix</Link>
        <Link href="/cuenta">Mi cuenta</Link>
      </nav>
      <h1 id="directory-title">{titulo}</h1>
      {children}
    </div>
  </main>;
}

function Fallo({ error, reintentar }: { error: unknown; reintentar: () => void }) {
  const api = error instanceof ErrorApi ? error : null;
  return <div className="identity-notice" role="alert">
    <p>{api?.status === 400 ? 'Los filtros o la página no son válidos. Revisa la búsqueda.'
      : 'No se pudo cargar la información. Vuelve a intentarlo.'}</p>
    {api?.problema.errors && <ul>{Object.entries(api.problema.errors).map(([campo, mensajes]) =>
      <li key={campo}>{mensajes.join(' ')}</li>)}</ul>}
    <button type="button" onClick={reintentar}>Volver a intentar</button>
    {api?.status === 400 && <p><Link href="/tecnicos">Reiniciar búsqueda</Link></p>}
    {api?.problema.traceId && <small>Referencia {api.problema.traceId}</small>}
  </div>;
}

function Reputacion({ tecnico }: { tecnico: Tecnico }) {
  return <p className="directory-reputation">{tecnico.cantidadValoraciones === 0 || tecnico.promedio === null
    ? 'Sin valoraciones'
    : `${tecnico.promedio.toLocaleString('es', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} de 5 · ${tecnico.cantidadValoraciones} ${tecnico.cantidadValoraciones === 1 ? 'valoración' : 'valoraciones'}`}</p>;
}

export function Directorio({ consulta }: { consulta: string }) {
  const router = useRouter();
  const filtros = new URLSearchParams(consulta);
  const [especialidad, setEspecialidad] = useState(filtros.get('especialidadId') ?? '');
  const [sector, setSector] = useState(filtros.get('sectorId') ?? '');
  const [catalogos, setCatalogos] = useState<{ especialidades: Especialidad[]; sectores: Sector[] } | null>(null);
  const [resultados, setResultados] = useState<Resultados | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [intento, setIntento] = useState(0);
  const [cargando, setCargando] = useState(true);

  useEffect(() => {
    const control = new AbortController();
    setCargando(true);
    setError(null);
    setResultados(null);
    Promise.all([
      solicitar<Resultados>(`/tecnicos${consulta ? `?${consulta}` : ''}`, { signal: control.signal }),
      solicitar<Especialidad[]>('/especialidades', { signal: control.signal }),
      solicitar<Sector[]>('/sectores', { signal: control.signal }),
    ]).then(([lista, especialidades, sectores]) => {
      if (control.signal.aborted) return;
      setResultados(lista);
      setCatalogos({ especialidades, sectores });
    }).catch((fallo: unknown) => {
      if (!control.signal.aborted) setError(fallo);
    }).finally(() => { if (!control.signal.aborted) setCargando(false); });
    return () => control.abort();
  }, [consulta, intento]);

  function buscar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const datos = new FormData(event.currentTarget);
    const siguiente = new URLSearchParams();
    for (const campo of ['especialidadId', 'sectorId', 'pageSize']) {
      const valor = datos.get(campo)?.toString();
      if (valor) siguiente.set(campo, valor);
    }
    router.push(`/tecnicos?${siguiente}`);
  }

  function pagina(numero: number) {
    const siguiente = new URLSearchParams(consulta);
    siguiente.set('page', numero.toString());
    return `/tecnicos?${siguiente}`;
  }

  return <Marco titulo="Encuentra un técnico">
    <form key={consulta} method="get" onSubmit={buscar} className="directory-filters" aria-label="Filtros de técnicos">
      <label>Especialidad<select name="especialidadId" value={especialidad} onChange={event => setEspecialidad(event.target.value)} disabled={!catalogos}>
        <option value="">Todas las especialidades</option>
        {catalogos?.especialidades.map(item => <option value={item.id} key={item.id}>{item.nombre}</option>)}
      </select></label>
      <label>Sector<select name="sectorId" value={sector} onChange={event => setSector(event.target.value)} disabled={!catalogos}>
        <option value="">Todos los sectores</option>
        {catalogos?.sectores.map(item => <option value={item.id} key={item.id}>{item.nombre} ({item.municipio})</option>)}
      </select></label>
      <label>Resultados por página<select name="pageSize" defaultValue={filtros.get('pageSize') ?? '20'}>
        <option value="10">10</option><option value="20">20</option><option value="50">50</option>
      </select></label>
      <button type="submit" className="identity-button" disabled={!catalogos}>Buscar técnicos</button>
      {consulta && <Link href="/tecnicos">Limpiar filtros</Link>}
    </form>
    <section aria-label="Resultados de técnicos" aria-busy={cargando}>
      {cargando && <p role="status">Buscando técnicos…</p>}
      {Boolean(error) && <Fallo error={error} reintentar={() => setIntento(intento + 1)} />}
      {resultados && <>
        <p role="status">{resultados.total} {resultados.total === 1 ? 'técnico encontrado' : 'técnicos encontrados'}</p>
        {resultados.items.length === 0 && <div className="directory-empty">
          <h2>{resultados.total === 0 ? 'No encontramos técnicos con estos filtros' : 'Esta página no tiene resultados'}</h2>
          <p>{resultados.total === 0 ? 'Prueba otra especialidad o un sector diferente.' : 'Regresa a la primera página para ver los perfiles disponibles.'}</p>
          <Link href={resultados.total === 0 ? '/tecnicos' : pagina(1)}>{resultados.total === 0 ? 'Ver todos los técnicos' : 'Ir a la primera página'}</Link>
        </div>}
        <ul className="directory-results">{resultados.items.map(tecnico => <li key={tecnico.id}>
          <article>
            <h2><Link href={`/tecnicos/${tecnico.id}`}>{tecnico.nombre}</Link></h2>
            <p className="directory-specialties">{tecnico.especialidades.map(item => item.nombre).join(', ')}</p>
            <p className="directory-summary">{tecnico.descripcion}</p>
            <p>Atiende en {tecnico.sectores.map(item => item.nombre).join(', ')}.</p>
            <Reputacion tecnico={tecnico} />
            <Link href={`/tecnicos/${tecnico.id}`}>Ver perfil profesional</Link>
          </article>
        </li>)}</ul>
        {(resultados.page > 1 || resultados.page * resultados.pageSize < resultados.total) && <nav className="directory-pagination" aria-label="Páginas de resultados">
          {resultados.page > 1 && <Link href={pagina(resultados.page - 1)}>Anterior</Link>}
          <span>Página {resultados.page}</span>
          {resultados.page * resultados.pageSize < resultados.total && <Link href={pagina(resultados.page + 1)}>Siguiente</Link>}
        </nav>}
      </>}
    </section>
  </Marco>;
}

export function PerfilPublico({ id }: { id: string }) {
  const [perfil, setPerfil] = useState<Tecnico | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [cargando, setCargando] = useState(true);
  const [intento, setIntento] = useState(0);

  useEffect(() => {
    const control = new AbortController();
    setCargando(true);
    setPerfil(null);
    setError(null);
    solicitar<Tecnico>(`/tecnicos/${encodeURIComponent(id)}`, { signal: control.signal })
      .then(actual => { if (!control.signal.aborted) setPerfil(actual); })
      .catch((fallo: unknown) => { if (!control.signal.aborted) setError(fallo); })
      .finally(() => { if (!control.signal.aborted) setCargando(false); });
    return () => control.abort();
  }, [id, intento]);

  const noDisponible = error instanceof ErrorApi && error.status === 404;
  return <Marco titulo={perfil?.nombre ?? (noDisponible ? 'Perfil no disponible' : 'Perfil profesional')}>
    <p><Link href="/tecnicos">Volver al buscador</Link></p>
    {cargando && <p role="status">Cargando perfil…</p>}
    {noDisponible && <p role="status">Este perfil no existe o ya no está publicado. Consulta los técnicos disponibles en el buscador.</p>}
    {Boolean(error) && !noDisponible && <Fallo error={error} reintentar={() => setIntento(intento + 1)} />}
    {perfil && <article className="directory-profile">
      <Reputacion tecnico={perfil} />
      <h2>Servicios</h2>
      <p className="directory-description">{perfil.descripcion}</p>
      <h2>Especialidades</h2>
      <ul>{perfil.especialidades.map(item => <li key={item.id}>{item.nombre}</li>)}</ul>
      <h2>Sectores que atiende</h2>
      <ul>{perfil.sectores.map(item => <li key={item.id}>{item.nombre} ({item.municipio})</li>)}</ul>
    </article>}
  </Marco>;
}
