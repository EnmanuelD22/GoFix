import type { Metadata } from 'next';
import { PerfilPublico } from '../../../features/professionals/public-directory';

export const metadata: Metadata = { title: 'Perfil del técnico' };

export default async function PerfilPublicoPage({ params }: { params: Promise<{ id: string }> }) {
  return <PerfilPublico id={(await params).id} />;
}
