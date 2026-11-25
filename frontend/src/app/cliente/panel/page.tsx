import type { Metadata } from 'next';
import { PanelCliente } from '../../../features/requests/client-panel';

export const metadata: Metadata = { title: 'Mi panel' };

export default function Panel() {
  return <PanelCliente />;
}
