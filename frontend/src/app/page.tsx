'use client';
import { useEffect, useState } from 'react';
import { solicitar } from '../features/auth/api';
import { Icon } from "../components/Icon";
import { SiteFooter } from "../components/SiteFooter";
import { SiteHeader } from "../components/SiteHeader";
const Tag = ({children,tone}: {children: React.ReactNode; tone: string}) => <span className={`tag tag--${tone}`}>{children}</span>;
const highlights=['Búsqueda por especialidad y sector','Seguimiento paso a paso del servicio','Una valoración por servicio completado'];
const steps=[{title:'Busca',text:'Filtra por especialidad y sector y compara perfiles publicados.'},{title:'Solicita',text:'Envía tu solicitud desde el perfil del técnico y coordina la visita.'},{title:'Sigue y valora',text:'Consulta el historial y comparte tu opinión al completar el servicio.'}];
type Opcion={id:number;nombre:string};

export default function HomePage() {
  const [specialties,setSpecialties]=useState<Opcion[]>([]);
  const [sectors,setSectors]=useState<Opcion[]>([]);
  const [failed,setFailed]=useState(false);
  useEffect(()=>{const controller=new AbortController();Promise.all([solicitar<Opcion[]>('/especialidades',{signal:controller.signal}),solicitar<Opcion[]>('/sectores',{signal:controller.signal})]).then(([a,b])=>{setSpecialties(a);setSectors(b)}).catch(()=>{if(!controller.signal.aborted)setFailed(true)});return ()=>controller.abort()},[]);
  return (
    <>
      <div className="theme-dark hero-band">
        <div className="container">
          <SiteHeader />
          <section className="hero" aria-labelledby="hero-title">
            <div>
              <Tag tone="night">Piloto en Santo Domingo de Guzmán</Tag>
              <h1 id="hero-title" className="hero__title">
                Tu <em>técnico de confianza</em>, a un clic de tu puerta.
              </h1>
              <p className="hero__lead muted">
                Encuentra especialistas de tu sector, solicita el servicio y sigue el trabajo hasta valorar la atención.
              </p>
              <form className="search" role="search" action="/tecnicos" method="get">
                <div className="field">
                  <label htmlFor="especialidad">Especialidad</label>
                  <select id="especialidad" name="especialidadId">
                    <option value="">Todas</option>
                    {specialties.map((s) => <option key={s.id} value={s.id}>{s.nombre}</option>)}
                  </select>
                </div>
                <div className="field">
                  <label htmlFor="sector">Sector</label>
                  <select id="sector" name="sectorId">
                    <option value="">Todos</option>
                    {sectors.map((s) => <option key={s.id} value={s.id}>{s.nombre}</option>)}
                  </select>
                </div>
                <button className="btn" type="submit"><Icon name="search" size={20} />Buscar</button>
              </form>
              {failed && <p className="catalog-notice" role="status">No pudimos cargar los filtros. Puedes abrir el directorio y reintentar allí.</p>}
              <ul className="checklist">
                {highlights.map((h) => (
                  <li key={h}><Icon name="check" size={20} />{h}</li>
                ))}
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

      <main id="contenido">
        <section className="container section" aria-labelledby="especialidades-title">
          <h2 id="especialidades-title" className="section__title">Todo tipo de reparaciones en un solo directorio</h2>
          <ul className="specialty-grid">
            {specialties.map((s) => (
              <li key={s.id}>
                <a className="card specialty-card" href={`/tecnicos?especialidadId=${s.id}`}>
                  <span className="icon-tile" data-tone="indigo"><Icon name="wrench" /></span>
                  <span><strong>{s.nombre}</strong><span className="muted">Ver técnicos</span></span>
                </a>
              </li>
            ))}
          </ul>
        </section>

        <section id="como-funciona" className="container section section--how" aria-labelledby="como-title">
          <h2 id="como-title" className="section__title">Cómo funciona</h2>
          <ol className="steps">
            {steps.map((step, i) => (
              <li key={step.title} className={`card step${i === 1 ? " step--featured" : ""}`}>
                <span className="step__num" aria-hidden="true">{`0${i + 1}`}</span>
                <h3 className="step__title">{step.title}</h3>
                <p>{step.text}</p>
              </li>
            ))}
          </ol>
        </section>

        <section className="container section--cta" aria-labelledby="cta-title">
          <div className="theme-dark cta-band">
            <div>
              <h2 id="cta-title">¿Eres técnico? Publica tu perfil.</h2>
              <p className="muted">Describe tu trabajo, elige especialidades y sectores, y recibe solicitudes de tu zona.</p>
            </div>
            <a className="btn" href="/registro">Registrarme como técnico<Icon name="arrow" size={20} /></a>
          </div>
        </section>
      </main>

      <SiteFooter />
    </>
  );
}
