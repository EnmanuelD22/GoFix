import type { Metadata } from 'next';
import { ListaSolicitudes } from '../../../features/requests/request-views';
import { consultaSolicitudes } from '../../../features/requests/request-query';

export const metadata: Metadata = { title: 'Solicitudes asignadas' };

export default async function SolicitudesTecnico({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  return <ListaSolicitudes rol="tecnico" consulta={await consultaSolicitudes(searchParams)} />;
}
