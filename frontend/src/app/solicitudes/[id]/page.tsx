import { DetalleSolicitud } from '../../../features/requests/request-views';

export default async function DetalleSolicitudPage({ params }: { params: Promise<{ id: string }> }) {
  return <DetalleSolicitud id={(await params).id} />;
}
