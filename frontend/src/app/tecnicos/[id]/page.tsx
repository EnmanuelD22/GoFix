import { PerfilPublico } from '../../../features/professionals/public-directory';

export default async function PerfilPublicoPage({ params }: { params: Promise<{ id: string }> }) {
  return <PerfilPublico id={(await params).id} />;
}
