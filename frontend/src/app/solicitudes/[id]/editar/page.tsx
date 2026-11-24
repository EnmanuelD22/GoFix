import { EditorSolicitud } from '../../../../features/requests/request-editor';

export default async function EditarSolicitud({ params }: { params: Promise<{ id: string }> }) {
  return <EditorSolicitud id={(await params).id} />;
}
