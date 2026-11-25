import type { Metadata } from 'next';
import { PanelTecnico } from '../../../features/requests/request-tracking';

export const metadata: Metadata = { title: 'Mi agenda' };

export default function Panel() {
  return <PanelTecnico />;
}
