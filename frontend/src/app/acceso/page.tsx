import type { Metadata } from 'next';
import { Acceso } from '../../features/auth/identity-views';

export const metadata: Metadata = { title: 'Iniciar sesión' };

type Parametros = { registro?: string; sesion?: string; volver?: string | string[] };

export default async function AccesoPage({ searchParams }: { searchParams: Promise<Parametros> }) {
  const params = await searchParams;
  return <Acceso registro={false} cuentaCreada={params.registro === 'ok'} sesionExpirada={params.sesion === 'expirada'}
    volver={typeof params.volver === 'string' ? params.volver : undefined} />;
}
