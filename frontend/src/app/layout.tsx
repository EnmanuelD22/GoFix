import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';
import localFont from 'next/font/local';
import './globals.css';

const bodyFont = localFont({ src: './fonts/Inter.ttf', variable: '--font-inter', display: 'swap' });
const displayFont = localFont({ src: './fonts/SpaceGrotesk.ttf', variable: '--font-space-grotesk', display: 'swap' });

export const metadata: Metadata = {
  title: { default: 'GoFix · Técnicos de confianza cerca de ti', template: '%s · GoFix' },
  description: 'Encuentra técnicos de tu zona, solicita el servicio y sigue el trabajo hasta valorarlo.',
};

export const viewport: Viewport = { themeColor: '#0F1228', colorScheme: 'light' };

export default function RootLayout({ children }: { children: ReactNode }) {
  return <html lang="es" className={`${bodyFont.variable} ${displayFont.variable}`}>
    <body>
      <a className="skip-link" href="#contenido">Saltar al contenido principal</a>
      {children}
    </body>
  </html>;
}
