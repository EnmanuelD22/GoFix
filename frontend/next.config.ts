import type { NextConfig } from 'next';

const target = new URL(process.env.GOFIX_API_TARGET ?? 'https://localhost:7043');
if (target.protocol !== 'https:' || target.username || target.password || target.pathname !== '/' || target.search || target.hash) {
  throw new Error('GOFIX_API_TARGET debe ser un origen HTTPS sin credenciales.');
}
const config: NextConfig = {
  async rewrites() {
    // El navegador conserva el origen de Next para cookies y llamadas a la API.
    return [{ source: '/api/:path*', destination: `${target.origin}/api/:path*` }];
  },
};
export default config;
