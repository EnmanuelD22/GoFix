import { EditorSolicitud } from '../../../../features/requests/request-editor';

export default async function NuevaSolicitud({ searchParams }: { searchParams: Promise<{ tecnico?: string | string[] }> }) {
  const { tecnico } = await searchParams;
  return <EditorSolicitud tecnico={typeof tecnico === 'string' ? tecnico : undefined} />;
}
