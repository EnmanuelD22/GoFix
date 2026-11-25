import Link from 'next/link';
import type { ReactNode } from 'react';
import { SiteFooter } from './SiteFooter';
import { SiteHeader } from './SiteHeader';

/** Marco de las páginas públicas: cabecera, migas de pan, contenido y pie. */
export function PublicLayout({ titulo, migas = [], cabecera, children, tituloId = 'page-title' }: {
  titulo: ReactNode;
  migas?: { href?: string; texto: string }[];
  cabecera?: ReactNode;
  children: ReactNode;
  tituloId?: string;
}) {
  return <>
    <div className="site-header-band"><div className="container"><SiteHeader bordered /></div></div>
    <main id="contenido" tabIndex={-1} className="container public-main" aria-labelledby={tituloId}>
      <div className="page-head">
        {migas.length > 0 && <nav aria-label="Ruta de navegación" className="breadcrumb">
          <ol>{migas.map((miga, indice) => <li key={indice}>
            {miga.href ? <Link href={miga.href}>{miga.texto}</Link> : <span aria-current="page">{miga.texto}</span>}
          </li>)}</ol>
        </nav>}
        <h1 id={tituloId}>{titulo}</h1>
        {cabecera}
      </div>
      {children}
    </main>
    <SiteFooter />
  </>;
}
