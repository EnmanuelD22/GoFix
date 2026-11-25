'use client';

import Link from 'next/link';
import { AppShell, useCuenta } from '../../components/AppShell';
import { Cargando, Vacio } from '../../components/Estados';
import { Fallo } from '../../components/Fallo';
import { Icon } from '../../components/Icon';
import { StatCard } from '../../components/StatCard';
import { plural } from '../../lib/formato';
import { useConsulta } from '../../lib/use-consulta';
import { TarjetaSolicitud, type PaginaSolicitudes } from './request-views';

type Total = { total: number };

function Resumen() {
  const { cuenta } = useCuenta();
  const pendientes = useConsulta<Total>('/solicitudes?estado=Pendiente&page=1&pageSize=1');
  const aceptadas = useConsulta<Total>('/solicitudes?estado=Aceptada&page=1&pageSize=1');
  const enProceso = useConsulta<Total>('/solicitudes?estado=EnProceso&page=1&pageSize=1');
  const completadas = useConsulta<Total>('/solicitudes?estado=Completada&page=1&pageSize=1');
  const recientes = useConsulta<PaginaSolicitudes>('/solicitudes?page=1&pageSize=5');
  const fallo = pendientes.error ?? aceptadas.error ?? enProceso.error ?? completadas.error;
  const primerNombre = cuenta.nombre.trim().split(/\s+/)[0];

  return <>
    <p className="welcome">Hola, <strong>{primerNombre}</strong>. Aquí tienes el estado de tus servicios.</p>
    <section aria-label="Resumen de tus solicitudes">
      <dl className="stats">
        <StatCard icono="clock" tono="amber" etiqueta="Pendientes" valor={pendientes.datos?.total} href="/cliente/solicitudes?estado=Pendiente"
          descripcion={plural(pendientes.datos?.total ?? 0, 'solicitud pendiente', 'solicitudes pendientes')} />
        <StatCard icono="cal" tono="mint" etiqueta="Aceptadas" valor={aceptadas.datos?.total} href="/cliente/solicitudes?estado=Aceptada"
          descripcion={plural(aceptadas.datos?.total ?? 0, 'solicitud aceptada', 'solicitudes aceptadas')} />
        <StatCard icono="play" tono="indigo" etiqueta="En proceso" valor={enProceso.datos?.total} href="/cliente/solicitudes?estado=EnProceso"
          descripcion={plural(enProceso.datos?.total ?? 0, 'servicio en proceso', 'servicios en proceso')} />
        <StatCard icono="check" tono="violet" etiqueta="Completadas" valor={completadas.datos?.total} href="/cliente/solicitudes?estado=Completada"
          descripcion={plural(completadas.datos?.total ?? 0, 'servicio completado', 'servicios completados')} />
      </dl>
      {fallo ? <Fallo error={fallo} enfocar={false} compacto reintentar={() => {
        pendientes.reintentar(); aceptadas.reintentar(); enProceso.reintentar(); completadas.reintentar();
      }} /> : null}
    </section>
    <div className="panel-grid">
      <section className="card panel" aria-labelledby="recientes-title">
        <div className="panel__head">
          <h2 id="recientes-title">Solicitudes recientes</h2>
          {recientes.datos && recientes.datos.total > 0 && <Link href="/cliente/solicitudes">Ver todas<Icon name="arrow" size={18} /></Link>}
        </div>
        {recientes.pendiente && <Cargando texto="Cargando solicitudes…" filas={3} />}
        {recientes.error ? <Fallo error={recientes.error} reintentar={recientes.reintentar} enfocar={false} /> : null}
        {recientes.datos && (recientes.datos.items.length === 0
          ? <Vacio titulo="Todavía no tienes solicitudes" accion={{ href: '/tecnicos', texto: 'Buscar un técnico' }}>
            Busca un técnico de tu zona y envíale tu primera solicitud.
          </Vacio>
          : <ul className="request-list request-list--compact">{recientes.datos.items.map(item =>
            <li key={item.id}><TarjetaSolicitud item={item} /></li>)}</ul>)}
      </section>
      <div className="panel-side">
        <section className="theme-dark card card--night" aria-labelledby="nueva-title">
          <span className="icon-tile" data-tone="indigo"><Icon name="search" /></span>
          <h2 id="nueva-title">¿Necesitas otro servicio?</h2>
          <p className="muted">Filtra por especialidad y sector, compara perfiles y envía tu solicitud.</p>
          <Link className="btn btn--block" href="/tecnicos">Buscar técnicos</Link>
        </section>
        {(completadas.datos?.total ?? 0) > 0 && <section className="card" aria-labelledby="valorar-title">
          <span className="icon-tile" data-tone="amber"><Icon name="star" /></span>
          <h2 id="valorar-title">Comparte tu experiencia</h2>
          <p className="muted">Valora tus servicios completados para ayudar a otros clientes a elegir.</p>
          <Link href="/cliente/solicitudes?estado=Completada">Ver servicios completados<Icon name="arrow" size={18} /></Link>
        </section>}
      </div>
    </div>
  </>;
}

export function PanelCliente() {
  return <AppShell rol="cliente" titulo="Mi panel" descripcion="Tus solicitudes y servicios, en un solo lugar."
    acciones={<Link className="btn" href="/tecnicos"><Icon name="plus" size={18} />Nueva solicitud</Link>}>
    <Resumen />
  </AppShell>;
}
