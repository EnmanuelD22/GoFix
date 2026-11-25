import type { Metadata } from 'next';
import { ListaSolicitudes } from '../../../features/requests/request-views';
import { consultaSolicitudes } from '../../../features/requests/request-query';

export const metadata: Metadata = { title: 'Mis solicitudes' };

export default async function SolicitudesCliente({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  return <ListaSolicitudes rol="cliente" consulta={await consultaSolicitudes(searchParams)} />;
}
