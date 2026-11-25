import { Acceso } from '../../features/auth/identity-views';

export default async function AccesoPage({ searchParams }: { searchParams: Promise<{ registro?: string; sesion?: string }> }) {
  const params = await searchParams;
  return <Acceso registro={false} cuentaCreada={params.registro === 'ok'} sesionExpirada={params.sesion === 'expirada'} />;
}
