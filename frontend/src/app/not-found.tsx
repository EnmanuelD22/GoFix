import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon } from '../components/Icon';
import { PublicLayout } from '../components/PublicLayout';

export const metadata: Metadata = { title: 'Página no encontrada' };

export default function NotFound() {
  return <PublicLayout titulo="No encontramos esta página">
    <section className="card status-page" aria-label="Opciones para continuar">
      <span className="status-page__code" aria-hidden="true">404</span>
      <p className="muted">Es posible que el enlace esté incompleto o que la página ya no exista. Puedes volver al inicio o buscar técnicos de tu zona.</p>
      <div className="button-row">
        <Link className="btn" href="/tecnicos"><Icon name="search" size={18} />Buscar técnicos</Link>
        <Link className="btn btn--ghost" href="/">Ir al inicio</Link>
      </div>
    </section>
  </PublicLayout>;
}
