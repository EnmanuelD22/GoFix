import Link from 'next/link';
import { Logo } from './Logo';

export function SiteFooter() {
  return <footer className="site-footer theme-dark">
    <div className="container">
      <div className="site-footer__grid">
        <div>
          <Logo />
          <p className="site-footer__about muted">Conectamos personas con técnicos de su zona. Piloto en Santo Domingo de Guzmán.</p>
        </div>
        <nav aria-label="Explorar">
          <h2>Explorar</h2>
          <ul>
            <li><Link href="/tecnicos">Buscar técnicos</Link></li>
            <li><Link href="/#como-funciona">Cómo funciona</Link></li>
            <li><Link href="/registro?rol=tecnico">Ofrecer mis servicios</Link></li>
          </ul>
        </nav>
        <nav aria-label="Tu cuenta">
          <h2>Tu cuenta</h2>
          <ul>
            <li><Link href="/acceso">Acceder</Link></li>
            <li><Link href="/registro">Crear cuenta</Link></li>
            <li><Link href="/cuenta">Mi cuenta</Link></li>
          </ul>
        </nav>
      </div>
      <div className="site-footer__bottom muted">
        <p>© {new Date().getFullYear()} GoFix</p>
        <p>Santo Domingo, República Dominicana</p>
      </div>
    </div>
  </footer>;
}
