import type { Metadata } from 'next';
import { EditorSolicitud } from '../../../../features/requests/request-editor';

export const metadata: Metadata = { title: 'Editar solicitud' };

export default async function EditarSolicitud({ params }: { params: Promise<{ id: string }> }) {
  return <EditorSolicitud id={(await params).id} />;
}
