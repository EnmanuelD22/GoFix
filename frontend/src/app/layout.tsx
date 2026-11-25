import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import localFont from 'next/font/local';
import './globals.css';

const bodyFont = localFont({ src: './fonts/Inter.ttf', variable: '--font-inter', display: 'swap' });
const displayFont = localFont({ src: './fonts/SpaceGrotesk.ttf', variable: '--font-space-grotesk', display: 'swap' });

export const metadata: Metadata = { title: 'GoFix', description: 'Servicios técnicos cerca de ti' };
export default function RootLayout({ children }: { children: ReactNode }) {
  return <html lang="es" className={`${bodyFont.variable} ${displayFont.variable}`}><body>{children}</body></html>;
}
