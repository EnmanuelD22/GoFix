'use client';

import Link from 'next/link';
import type { ReactNode } from 'react';
import { Icon } from '../../components/Icon';
import { SiteFooter } from '../../components/SiteFooter';
import { SiteHeader } from '../../components/SiteHeader';
import { inicioPorRol } from '../../lib/navegacion';
import { useSesionOpcional } from '../auth/session';
import { estiloEspecialidad, useCatalogos } from '../catalogs/catalogos';

const Tag = ({ children, tone }: { children: ReactNode; tone: string }) => <span className={`tag tag--${tone}`}>{children}</span>;

const highlights = ['Búsqueda por especialidad y sector', 'Seguimiento paso a paso del servicio', 'Una valoración por servicio completado'];
const steps = [
  { title: 'Busca', text: 'Filtra por especialidad y sector y compara perfiles publicados.' },
  { title: 'Solicita', text: 'Envía tu solicitud desde el perfil del técnico y coordina la visita.' },
  { title: 'Sigue y valora', text: 'Consulta el historial y comparte tu opinión al completar el servicio.' },
];

export function Inicio() {
  const catalogos = useCatalogos();
  const { cuenta } = useSesionOpcional();
  const especialidades = catalogos.datos?.especialidades ?? [];
  const sectores = catalogos.datos?.sectores ?? [];

  return <>
    <div className="theme-dark hero-band">
      <div className="container">
        <SiteHeader />
        <section className="hero" aria-labelledby="hero-title">
          <div>
            <Tag tone="night">Piloto en Santo Domingo de Guzmán</Tag>
            <h1 id="hero-title" className="hero__title">Tu <em>técnico de confianza</em>, a un clic de tu puerta.</h1>
            <p className="hero__lead muted">Encuentra especialistas de tu sector, solicita el servicio y sigue el trabajo hasta valorar la atención.</p>
            <form className="search" role="search" aria-label="Buscar técnicos" action="/tecnicos" method="get">
              <div className="field">
                <label htmlFor="especialidad">Especialidad</label>
                <select id="especialidad" name="especialidadId" aria-busy={catalogos.pendiente}>
                  <option value="">Todas</option>
                  {especialidades.map(item => <option key={item.id} value={item.id}>{item.nombre}</option>)}
                </select>
              </div>
              <div className="field">
                <label htmlFor="sector">Sector</label>
                <select id="sector" name="sectorId" aria-busy={catalogos.pendiente}>
                  <option value="">Todos</option>
                  {sectores.map(item => <option key={item.id} value={item.id}>{item.nombre}</option>)}
                </select>
              </div>
              <button className="btn" type="submit"><Icon name="search" size={20} />Buscar</button>
            </form>
            {Boolean(catalogos.error) && <p className="catalog-notice" role="status">
              No pudimos cargar los filtros. <button type="button" className="link-button" onClick={catalogos.reintentar}>Reintentar</button> o abre el <Link href="/tecnicos">directorio completo</Link>.
            </p>}
            <ul className="checklist">
              {highlights.map(item => <li key={item}><Icon name="check" size={20} />{item}</li>)}
            </ul>
          </div>
          <div className="hero__visual" aria-hidden="true">
            <article className="card float-card float-card--profile">
              <div className="profile">
                <span className="avatar">T1</span>
                <div>
                  <h3>Tu próximo especialista</h3>
                  <p className="muted">Consulta perfiles y cobertura</p>
                </div>
              </div>
              <p className="rating"><Icon name="star" size={16} filled /><span className="muted">Opiniones de servicios completados</span></p>
              <Tag tone="success">Perfil publicado</Tag>
            </article>
            <article className="card float-card float-card--status">
              <h3>Todo el servicio, conectado</h3>
              <p>Solicita, coordina y consulta cada cambio desde tu cuenta.</p>
              <Tag tone="solid">Seguimiento del servicio</Tag>
            </article>
          </div>
        </section>
      </div>
    </div>

    <main id="contenido" tabIndex={-1}>
      <section className="container section" aria-labelledby="especialidades-title">
        <div className="section__head">
          <h2 id="especialidades-title" className="section__title">Todo tipo de reparaciones en un solo directorio</h2>
          <Link className="btn btn--ghost" href="/tecnicos">Ver todos los técnicos<Icon name="arrow" size={18} /></Link>
        </div>
        {catalogos.pendiente && <>
          <p role="status" className="sr-only">Cargando especialidades…</p>
          <div className="specialty-grid" aria-hidden="true">{[0, 1, 2, 3, 4, 5].map(indice => <div key={indice} className="skeleton skeleton--card" />)}</div>
        </>}
        {Boolean(catalogos.error) && <p className="muted">Las especialidades no están disponibles en este momento.</p>}
        {especialidades.length > 0 && <ul className="specialty-grid">
          {especialidades.map(item => {
            const estilo = estiloEspecialidad(item.nombre);
            return <li key={item.id}>
              <Link className="card specialty-card" href={`/tecnicos?especialidadId=${item.id}`}>
                <span className="icon-tile" data-tone={estilo.tone}><Icon name={estilo.icon} /></span>
                <span><strong>{item.nombre}</strong><span className="muted">Ver técnicos</span></span>
                <Icon name="arrow" size={18} className="specialty-card__arrow" />
              </Link>
            </li>;
          })}
        </ul>}
      </section>

      <section id="como-funciona" className="container section section--how" aria-labelledby="como-title">
        <h2 id="como-title" className="section__title">Cómo funciona</h2>
        <ol className="steps">
          {steps.map((step, indice) => <li key={step.title} className={`card step${indice === 1 ? ' step--featured' : ''}`}>
            <span className="step__num" aria-hidden="true">{`0${indice + 1}`}</span>
            <h3 className="step__title">{step.title}</h3>
            <p>{step.text}</p>
          </li>)}
        </ol>
      </section>

      <section className="container section--cta" aria-labelledby="cta-title">
        <div className="theme-dark cta-band">
          {cuenta?.rol === 'tecnico' ? <>
            <div>
              <h2 id="cta-title">Tu perfil profesional, al día.</h2>
              <p className="muted">Mantén tu descripción y cobertura actualizadas para recibir solicitudes de tu zona.</p>
            </div>
            <Link className="btn" href={inicioPorRol('tecnico')}>Ir a mi agenda<Icon name="arrow" size={20} /></Link>
          </> : <>
            <div>
              <h2 id="cta-title">¿Eres técnico? Publica tu perfil.</h2>
              <p className="muted">Describe tu trabajo, elige especialidades y sectores, y recibe solicitudes de tu zona.</p>
            </div>
            <Link className="btn" href={cuenta ? '/tecnicos' : '/registro?rol=tecnico'}>
              {cuenta ? 'Explorar técnicos' : 'Registrarme como técnico'}<Icon name="arrow" size={20} />
            </Link>
          </>}
        </div>
      </section>
    </main>

    <SiteFooter />
  </>;
}
