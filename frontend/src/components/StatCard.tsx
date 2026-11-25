import Link from 'next/link';
import { Icon, type IconName } from './Icon';

/** Indicador del panel; debe ubicarse dentro de un <dl className="stats">. */
export function StatCard({ icono, tono, etiqueta, valor, href, descripcion }: {
  icono: IconName;
  tono: string;
  etiqueta: string;
  valor: number | undefined;
  href: string;
  /** Nombre accesible completo del enlace, por ejemplo «1 solicitud pendiente». */
  descripcion: string;
}) {
  return <div className="card stat">
    <dt className="muted"><span className="icon-tile" data-tone={tono}><Icon name={icono} /></span>{etiqueta}</dt>
    <dd>{valor === undefined
      ? <span className="skeleton skeleton--number" role="img" aria-label={'Cargando ' + etiqueta.toLowerCase()} />
      : <Link href={href} aria-label={descripcion}>{valor}</Link>}</dd>
  </div>;
}
