'use client';

import { useRef, useState, type FormEvent } from 'react';
import { ErrorApi, guardar } from '../auth/api';
import { Fallo, useConsulta } from '../requests/request-views';

type Valoracion = { id: string; puntuacion: number; comentario: string | null; createdAtUtc: string };
type Opinion = Valoracion & { nombreCliente: string };
type Pagina = { items: Opinion[]; page: number; pageSize: number; total: number; promedio: number | null; cantidad: number };

function Fecha({ valor }: { valor: string }) {
  return <time dateTime={valor}>{new Date(valor).toLocaleDateString('es')}</time>;
}

export function ValorarServicio({ id }: { id: string }) {
  const ruta = '/solicitudes/' + encodeURIComponent(id) + '/valoracion';
  const consulta = useConsulta<Valoracion | undefined>(ruta);
  const [creada, setCreada] = useState<Valoracion>();
  const [error, setError] = useState<unknown>(null);
  const [ocupado, setOcupado] = useState(false);
  const [datos, setDatos] = useState<{ puntuacion: number; comentario: string }>();
  const dialogo = useRef<HTMLDialogElement>(null);
  const enCurso = useRef(false);
  const valoracion = creada ?? consulta.datos;

  function preparar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    const form = new FormData(evento.currentTarget);
    setDatos({ puntuacion: Number(form.get('puntuacion')), comentario: String(form.get('comentario')).trim() });
    dialogo.current?.showModal();
  }

  async function publicar() {
    if (!datos || enCurso.current) return;
    enCurso.current = true;
    setOcupado(true);
    setError(null);
    try {
      setCreada(await guardar<Valoracion>(ruta, 'POST', datos));
    } catch (fallo) {
      setError(fallo);
      // Una respuesta perdida o un duplicado se comprueba antes de ofrecer otro envío.
      consulta.reintentar();
    } finally {
      dialogo.current?.close();
      setOcupado(false);
      enCurso.current = false;
    }
  }

  return <section aria-labelledby="valoracion-titulo">
    <h2 id="valoracion-titulo">Tu valoración</h2>
    {consulta.pendiente && <p role="status">Consultando valoración…</p>}
    {consulta.error ? <Fallo error={consulta.error} reintentar={consulta.reintentar} /> : null}
    {error && !valoracion ? error instanceof ErrorApi && error.status === 409
      ? <p role="alert">La valoración ya existe o el servicio cambió. Consulta de nuevo el detalle.</p>
      : <Fallo error={error} /> : null}
    {valoracion ? <div>
      <p role="status">Valoración publicada. Esta opinión queda guardada de forma definitiva.</p>
      <p>{valoracion.puntuacion} de 5 estrellas · <Fecha valor={valoracion.createdAtUtc} /></p>
      {valoracion.comentario && <p className="directory-description">{valoracion.comentario}</p>}
    </div> : !consulta.pendiente && !consulta.error && <form className="requests-form" onSubmit={preparar}>
      <fieldset disabled={ocupado}>
        <legend>¿Cómo fue el servicio?</legend>
        <label>Puntuación<select name="puntuacion" required defaultValue="">
          <option value="">Elige una puntuación</option>
          {[1, 2, 3, 4, 5].map(valor => <option key={valor} value={valor}>{'★'.repeat(valor)} — {valor} de 5</option>)}
        </select></label>
        <label>Comentario opcional<textarea name="comentario" rows={4} maxLength={1000} /></label>
        <p>Tu nombre de pila, puntuación y comentario serán públicos.</p>
        <button className="identity-button" type="submit">Publicar valoración</button>
      </fieldset>
    </form>}
    <dialog ref={dialogo} aria-labelledby="valoracion-confirmar" onCancel={evento => { if (ocupado) evento.preventDefault(); }}>
      <h2 id="valoracion-confirmar">¿Publicar tu valoración?</h2>
      <p>Se publicará una puntuación de {datos?.puntuacion} de 5. La opinión quedará guardada de forma definitiva.</p>
      <div className="requests-actions">
        <button type="button" autoFocus disabled={ocupado} onClick={() => dialogo.current?.close()}>Revisar opinión</button>
        <button type="button" disabled={ocupado} onClick={publicar}>{ocupado ? 'Publicando…' : 'Confirmar publicación'}</button>
      </div>
    </dialog>
  </section>;
}

export function OpinionesPublicas({ tecnicoId }: { tecnicoId: string }) {
  const [page, setPage] = useState(1);
  const consulta = useConsulta<Pagina>('/tecnicos/' + encodeURIComponent(tecnicoId) + '/valoraciones?page=' + page + '&pageSize=10');
  const lista = consulta.datos;
  return <section className="reviews-public" aria-labelledby="opiniones-titulo">
    <h2 id="opiniones-titulo">Opiniones de clientes</h2>
    {consulta.pendiente && <p role="status">Cargando opiniones…</p>}
    {consulta.error ? <Fallo error={consulta.error} reintentar={consulta.reintentar} /> : null}
    {lista && <>
      <p role="status">{lista.cantidad === 0 ? 'Sin valoraciones'
        : lista.promedio?.toLocaleString('es', { minimumFractionDigits: 1, maximumFractionDigits: 1 }) + ' de 5 · ' + lista.cantidad + (lista.cantidad === 1 ? ' valoración' : ' valoraciones')}</p>
      {lista.items.length === 0 && lista.total > 0 && <p>Esta página no tiene opiniones.</p>}
      <ul className="directory-results">{lista.items.map(item => <li key={item.id}>
        <article>
          <h3>{item.nombreCliente}</h3>
          <p>{item.puntuacion} de 5 estrellas · <Fecha valor={item.createdAtUtc} /></p>
          {item.comentario && <p className="directory-description">{item.comentario}</p>}
        </article>
      </li>)}</ul>
      {(page > 1 || page * lista.pageSize < lista.total) && <nav className="directory-pagination" aria-label="Páginas de opiniones">
        {page > 1 && <button type="button" onClick={() => setPage(page - 1)}>Opiniones anteriores</button>}
        <span>Página {page}</span>
        {page * lista.pageSize < lista.total && <button type="button" onClick={() => setPage(page + 1)}>Más opiniones</button>}
      </nav>}
    </>}
  </section>;
}
