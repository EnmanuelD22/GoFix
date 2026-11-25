import type { Metadata } from 'next';
import { Acceso } from '../../features/auth/identity-views';

export const metadata: Metadata = { title: 'Crear cuenta' };

type Parametros = { rol?: string | string[]; volver?: string | string[] };

export default async function RegistroPage({ searchParams }: { searchParams: Promise<Parametros> }) {
  const params = await searchParams;
  return <Acceso registro rolInicial={params.rol === 'tecnico' ? 'tecnico' : 'cliente'}
    volver={typeof params.volver === 'string' ? params.volver : undefined} />;
}
