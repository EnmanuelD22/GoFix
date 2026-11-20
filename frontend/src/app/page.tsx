import Link from 'next/link';

export default function Home() {
  return (
    <main className="intro" aria-labelledby="intro-title">
      <div className="intro__content">
        <p className="brand">GoFix</p>
        <h1 id="intro-title">Servicios técnicos cerca de ti</h1>
        <p className="intro__description">
          Una plataforma para conectar a personas que necesitan asistencia en casa con técnicos de su zona.
        </p>
        <nav className="intro__actions" aria-label="Explorar GoFix">
          <Link className="identity-button" href="/tecnicos">Buscar técnicos</Link>
          <Link href="/registro">Crear cuenta</Link>
          <Link href="/cuenta">Mi cuenta</Link>
        </nav>
      </div>
    </main>
  );
}
