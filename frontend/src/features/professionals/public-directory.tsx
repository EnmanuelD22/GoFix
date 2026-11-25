'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Aviso, Cargando, Vacio } from '../../components/Estados';
import { Estrellas } from '../../components/Estrellas';
import { Fallo } from '../../components/Fallo';
import { Icon } from '../../components/Icon';
import { PublicLayout } from '../../components/PublicLayout';
import { iniciales, plural, promedioVisible } from '../../lib/formato';
import { rutaAcceso } from '../../lib/navegacion';
import { useConsulta } from '../../lib/use-consulta';
import { ErrorApi } from '../auth/api';
import { useSesionOpcional } from '../auth/session';
import { estiloEspecialidad, useCatalogos, type Especialidad, type Sector } from '../catalogs/catalogos';
import { OpinionesPublicas } from '../reviews/review-views';

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

const mensajesBusqueda = { 400: 'Los filtros o la página no son válidos. Revisa la búsqueda.' };

function Reputacion({ tecnico }: { tecnico: Tecnico }) {
  const disponible = tecnico.cantidadValoraciones > 0 && tecnico.promedio !== null;
  return <p className="rating tech-card__rating">{disponible ? <>
    <Estrellas valor={tecnico.promedio!} />
    <b>{promedioVisible(tecnico.promedio!)} de 5</b>
    <span className="muted">· {plural(tecnico.cantidadValoraciones, 'valoración', 'valoraciones')}</span>
  </> : <span className="muted">Sin valoraciones</span>}</p>;
}

function TarjetaTecnico({ tecnico }: { tecnico: Tecnico }) {
  const estilo = estiloEspecialidad(tecnico.especialidades[0]?.nombre ?? '');
  const enlace = `/tecnicos/${tecnico.id}`;
  return <article className="card tech-card">
    <div className="tech-card__banner" data-tone={estilo.tone}>
      <span className="tech-card__avatar" aria-hidden="true">{iniciales(tecnico.nombre)}</span>
      <Icon name={estilo.icon} size={28} />
    </div>
    <div className="tech-card__body">
      <h2><Link href={enlace}>{tecnico.nombre}</Link></h2>
      <ul className="tag-list" aria-label="Especialidades">{tecnico.especialidades.map(item =>
        <li key={item.id}><span className="tag tag--info">{item.nombre}</span></li>)}</ul>
      <p className="directory-summary">{tecnico.descripcion}</p>
      <p className="tech-card__sectors muted"><Icon name="pin" size={16} /><span className="sr-only">Sectores: </span>
        {tecnico.sectores.map(item => item.nombre).join(' · ')}</p>
      <Reputacion tecnico={tecnico} />
      <Link className="btn btn--block" href={enlace} aria-label={'Ver perfil profesional de ' + tecnico.nombre}>Ver perfil profesional</Link>
    </div>
  </article>;
}

export function Directorio({ consulta }: { consulta: string }) {
  const router = useRouter();
  const filtros = new URLSearchParams(consulta);
  const [especialidad, setEspecialidad] = useState(filtros.get('especialidadId') ?? '');
  const [sector, setSector] = useState(filtros.get('sectorId') ?? '');
  const catalogos = useCatalogos();
  const resultados = useConsulta<Resultados>(`/tecnicos${consulta ? `?${consulta}` : ''}`);
  const lista = resultados.datos;

  function buscar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const datos = new FormData(event.currentTarget);
    const siguiente = new URLSearchParams();
    for (const campo of ['especialidadId', 'sectorId', 'pageSize']) {
      const valor = datos.get(campo)?.toString();
      if (valor) siguiente.set(campo, valor);
    }
    router.push(`/tecnicos${siguiente.size ? '?' + siguiente : ''}`);
  }

  function pagina(numero: number) {
    const siguiente = new URLSearchParams(consulta);
    siguiente.set('page', numero.toString());
    return `/tecnicos?${siguiente}`;
  }

  const sectores = catalogos.datos?.sectores ?? [];
  const municipios = [...new Set(sectores.map(item => item.municipio))];
  const municipio = sectores.find(item => String(item.id) === sector)?.municipio ?? (municipios.length === 1 ? municipios[0] : null);
  const totalPaginas = lista ? Math.ceil(lista.total / lista.pageSize) : 0;
  const paginas = lista ? Array.from({ length: Math.min(5, totalPaginas) }, (_, indice) =>
    Math.max(1, Math.min(lista.page - 2, totalPaginas - 4)) + indice) : [];
  const filtrosActivos = [
    catalogos.datos?.especialidades.find(item => String(item.id) === filtros.get('especialidadId'))?.nombre,
    sectores.find(item => String(item.id) === filtros.get('sectorId'))?.nombre,
  ].filter(Boolean);

  return <PublicLayout titulo="Encuentra un técnico" tituloId="directory-title"
    migas={[{ href: '/', texto: 'Inicio' }, { texto: 'Técnicos' }]}
    cabecera={<>
      {municipio && <p className="directory-location"><Icon name="pin" size={18} />Técnicos en {municipio}</p>}
      <p className="page-head__lead muted">Busca por especialidad y sector. Tus filtros quedan guardados en la dirección de la página para compartirlos o volver más tarde.</p>
    </>}>
    <div className="directory-layout">
      <aside aria-label="Filtros">
        <form key={consulta} method="get" onSubmit={buscar} className="card filters" aria-label="Filtros de técnicos">
          <h2>Filtrar</h2>
          <div className="field"><label htmlFor="filtro-especialidad">Especialidad</label>
            <select id="filtro-especialidad" name="especialidadId" value={especialidad} onChange={event => setEspecialidad(event.target.value)} disabled={!catalogos.datos}>
              <option value="">Todas las especialidades</option>
              {catalogos.datos?.especialidades.map(item => <option value={item.id} key={item.id}>{item.nombre}</option>)}
            </select></div>
          <div className="field"><label htmlFor="filtro-sector">Sector</label>
            <select id="filtro-sector" name="sectorId" value={sector} onChange={event => setSector(event.target.value)} disabled={!catalogos.datos}>
              <option value="">Todos los sectores</option>
              {sectores.map(item => <option value={item.id} key={item.id}>{item.nombre} ({item.municipio})</option>)}
            </select></div>
          <div className="field"><label htmlFor="filtro-tamano">Resultados por página</label>
            <select id="filtro-tamano" name="pageSize" defaultValue={filtros.get('pageSize') ?? '20'}>
              <option value="10">10</option><option value="20">20</option><option value="50">50</option>
            </select></div>
          <button type="submit" className="btn" disabled={!catalogos.datos}><Icon name="search" size={18} />Buscar técnicos</button>
          {consulta && <Link className="btn btn--ghost" href="/tecnicos">Limpiar filtros</Link>}
          {Boolean(catalogos.error) && <Fallo error={catalogos.error} reintentar={catalogos.reintentar} enfocar={false} compacto />}
        </form>
      </aside>
      <section aria-labelledby="resultados-title" aria-busy={resultados.pendiente || resultados.recargando}>
        <h2 id="resultados-title" className="sr-only">Resultados de técnicos</h2>
        {resultados.pendiente && <>
          <p role="status" className="loading__text"><span className="spinner" aria-hidden="true" />Buscando técnicos…</p>
          <div className="tech-grid" aria-hidden="true">{[0, 1, 2, 3].map(indice => <div key={indice} className="skeleton skeleton--tech" />)}</div>
        </>}
        {resultados.error ? <Fallo error={resultados.error} reintentar={resultados.reintentar} mensajes={mensajesBusqueda} /> : null}
        {resultados.error instanceof ErrorApi && resultados.error.status === 400 && <p><Link href="/tecnicos">Reiniciar búsqueda</Link></p>}
        {lista && <>
          <div className="results-bar">
            <p role="status"><strong>{plural(lista.total, 'técnico encontrado', 'técnicos encontrados')}</strong>
              {filtrosActivos.length > 0 && <span className="muted"> · {filtrosActivos.join(' · ')}</span>}</p>
            {totalPaginas > 1 && <p className="muted">Página {lista.page} de {totalPaginas}</p>}
          </div>
          {lista.items.length === 0 && (lista.total === 0
            ? <Vacio icono="search" titulo="No encontramos técnicos con estos filtros" accion={{ href: '/tecnicos', texto: 'Ver todos los técnicos' }}>
              Prueba otra especialidad o un sector diferente.
            </Vacio>
            : <Vacio titulo="Esta página no tiene resultados" accion={{ href: pagina(1), texto: 'Ir a la primera página' }}>
              Regresa a la primera página para ver los perfiles disponibles.
            </Vacio>)}
          <ul className="tech-grid directory-results">{lista.items.map(tecnico => <li key={tecnico.id}><TarjetaTecnico tecnico={tecnico} /></li>)}</ul>
          {totalPaginas > 1 && <nav className="pagination" aria-label="Páginas de resultados">
            {lista.page > 1 && <Link className="btn btn--ghost" href={pagina(lista.page - 1)}><Icon name="back" size={18} />Anterior</Link>}
            {paginas.map(numero => <Link key={numero} className={numero === lista.page ? 'btn' : 'btn btn--ghost'} href={pagina(numero)}
              aria-current={numero === lista.page ? 'page' : undefined} aria-label={`Página ${numero}`}>{numero}</Link>)}
            {lista.page < totalPaginas && <Link className="btn btn--ghost" href={pagina(lista.page + 1)}>Siguiente<Icon name="arrow" size={18} /></Link>}
          </nav>}
        </>}
      </section>
    </div>
  </PublicLayout>;
}

function AccionSolicitar({ tecnico }: { tecnico: Tecnico }) {
  const { cuenta, cargando } = useSesionOpcional();
  const nueva = '/cliente/solicitudes/nueva?tecnico=' + encodeURIComponent(tecnico.id);
  if (cargando) return <span className="skeleton skeleton--button" aria-hidden="true" />;
  if (cuenta?.rol === 'tecnico') {
    return cuenta.perfilTecnicoId === tecnico.id
      ? <Link className="btn btn--block" href="/tecnico/perfil"><Icon name="edit" size={18} />Editar mi perfil</Link>
      : <p className="muted request-note">Las solicitudes se envían desde una cuenta de cliente.</p>;
  }
  return <>
    <Link className="btn btn--block request-start" href={cuenta ? nueva : rutaAcceso(nueva)}><Icon name="send" size={18} />Solicitar servicio</Link>
    {!cuenta && <p className="muted request-note">Inicia sesión o <Link href={'/registro?volver=' + encodeURIComponent(nueva)}>crea una cuenta de cliente</Link> para enviar la solicitud.</p>}
  </>;
}

export function PerfilPublico({ id }: { id: string }) {
  const consulta = useConsulta<Tecnico>(`/tecnicos/${encodeURIComponent(id)}`);
  const perfil = consulta.datos;
  const noDisponible = consulta.error instanceof ErrorApi && consulta.error.status === 404;
  const estilo = estiloEspecialidad(perfil?.especialidades[0]?.nombre ?? '');

  return <PublicLayout titulo={perfil?.nombre ?? (noDisponible ? 'Perfil no disponible' : 'Perfil profesional')}
    migas={[{ href: '/', texto: 'Inicio' }, { href: '/tecnicos', texto: 'Técnicos' }, { texto: perfil?.nombre ?? 'Perfil' }]}
    cabecera={perfil && <div className="profile-hero__meta">
      <Reputacion tecnico={perfil} />
      <ul className="tag-list" aria-label="Especialidades">{perfil.especialidades.map(item =>
        <li key={item.id}><span className="tag tag--info">{item.nombre}</span></li>)}</ul>
    </div>}>
    {consulta.pendiente && <Cargando texto="Cargando perfil…" filas={3} />}
    {noDisponible && <Vacio icono="user" titulo="Este perfil no existe o ya no está publicado" accion={{ href: '/tecnicos', texto: 'Ver técnicos disponibles' }}>
      Consulta los técnicos disponibles en el buscador.
    </Vacio>}
    {consulta.error && !noDisponible ? <Fallo error={consulta.error} reintentar={consulta.reintentar} /> : null}
    {perfil && <div className="profile-layout">
      <div className="profile-layout__main">
        <section className="card public-profile" aria-labelledby="servicios-title">
          <h2 id="servicios-title">Sobre sus servicios</h2>
          <p className="directory-description">{perfil.descripcion}</p>
        </section>
        <div className="card">
          <OpinionesPublicas key={perfil.id} tecnicoId={perfil.id} />
        </div>
      </div>
      <aside className="profile-layout__aside" aria-label="Resumen y contacto">
        <div className="card profile-card">
          <div className="profile-card__banner" data-tone={estilo.tone}>
            <span className="tech-card__avatar" aria-hidden="true">{iniciales(perfil.nombre)}</span>
            <Icon name={estilo.icon} size={28} />
          </div>
          <AccionSolicitar tecnico={perfil} />
          <h2>Sectores que atiende</h2>
          <ul className="coverage-list">{perfil.sectores.map(item => <li key={item.id}><Icon name="pin" size={16} /><span>{item.nombre}<span className="muted"> · {item.municipio}</span></span></li>)}</ul>
          <Aviso><p>El teléfono del técnico se comparte contigo cuando acepta tu solicitud.</p></Aviso>
        </div>
      </aside>
    </div>}
  </PublicLayout>;
}
