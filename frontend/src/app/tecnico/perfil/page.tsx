import type { Metadata } from 'next';
import { PerfilProfesional } from '../../../features/professionals/profile-view';

export const metadata: Metadata = { title: 'Mi perfil profesional' };

export default function PerfilPage() {
  return <PerfilProfesional />;
}
