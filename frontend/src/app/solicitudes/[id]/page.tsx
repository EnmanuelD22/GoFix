import type { Metadata } from 'next';
import { DetalleSolicitud } from '../../../features/requests/request-views';

export const metadata: Metadata = { title: 'Detalle de solicitud' };

export default async function DetalleSolicitudPage({ params }: { params: Promise<{ id: string }> }) {
  return <DetalleSolicitud id={(await params).id} />;
}
