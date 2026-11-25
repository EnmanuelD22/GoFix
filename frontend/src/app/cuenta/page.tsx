import type { Metadata } from 'next';
import { MiCuenta } from '../../features/auth/identity-views';

export const metadata: Metadata = { title: 'Mi cuenta' };

export default function CuentaPage() {
  return <MiCuenta />;
}
