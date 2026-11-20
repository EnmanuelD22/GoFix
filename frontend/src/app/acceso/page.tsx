import { Acceso } from '../../features/auth/identity-views';

export default async function AccesoPage({ searchParams }: { searchParams: Promise<{ registro?: string }> }) {
  return <Acceso registro={false} cuentaCreada={(await searchParams).registro === 'ok'} />;
}
