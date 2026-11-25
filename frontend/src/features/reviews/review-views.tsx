'use client';

import { useId, useRef, useState, type FormEvent } from 'react';
import { Campo } from '../../components/Campo';
import { Aviso, Cargando } from '../../components/Estados';
import { Estrellas } from '../../components/Estrellas';
import { erroresDeCampo, Fallo } from '../../components/Fallo';
import { Icon } from '../../components/Icon';
import { fechaCorta, plural, promedioVisible } from '../../lib/formato';
import { useConsulta } from '../../lib/use-consulta';
import { ErrorApi, guardar } from '../auth/api';

type Valoracion = { id: string; puntuacion: number; comentario: string | null; createdAtUtc: string };
type Opinion = Valoracion & { nombreCliente: string };
type Pagina = { items: Opinion[]; page: number; pageSize: number; total: number; promedio: number | null; cantidad: number };

const textosPuntuacion = ['', 'Muy malo', 'Malo', 'Aceptable', 'Bueno', 'Excelente'];

function Fecha({ valor }: { valor: string }) {
  return <time dateTime={valor}>{fechaCorta(valor)}</time>;
}

function SelectorEstrellas({ valor, cambiar, error }: { valor: number; cambiar: (valor: number) => void; error: unknown }) {
  const nombre = useId();
  const mensajes = erroresDeCampo(error).puntuacion;
  return <fieldset className="star-input" aria-describedby={mensajes ? nombre + '-error' : nombre + '-ayuda'} aria-invalid={mensajes ? true : undefined}>
    <legend>Puntuación</legend>
    <div className="star-input__options">
      {[1, 2, 3, 4, 5].map(numero => <label key={numero} className={numero <= valor ? 'is-on' : undefined}>
        <input type="radio" name="puntuacion" value={numero} checked={valor === numero} onChange={() => cambiar(numero)} required />
        <Icon name="star" size={32} filled={numero <= valor} />
        <span className="sr-only">{plural(numero, 'estrella', 'estrellas')}: {textosPuntuacion[numero]}</span>
      </label>)}
    </div>
    <p id={nombre + '-ayuda'} className="field__hint" aria-live="polite">{valor ? `${valor} de 5 · ${textosPuntuacion[valor]}` : 'Elige de 1 a 5 estrellas.'}</p>
    {mensajes && <p id={nombre + '-error'} className="field__error"><Icon name="alert" size={16} />{mensajes.join(' ')}</p>}
  </fieldset>;
}

export function ValorarServicio({ id }: { id: string }) {
  const avisoId = useId();
  const ruta = '/solicitudes/' + encodeURIComponent(id) + '/valoracion';
  const consulta = useConsulta<Valoracion | undefined>(ruta);
  const [creada, setCreada] = useState<Valoracion>();
  const [error, setError] = useState<unknown>(null);
  const [ocupado, setOcupado] = useState(false);
  const [puntuacion, setPuntuacion] = useState(0);
  const [comentario, setComentario] = useState('');
  const dialogo = useRef<HTMLDialogElement>(null);
  const enCurso = useRef(false);
  const valoracion = creada ?? consulta.datos;

  function preparar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    if (!puntuacion) return;
    dialogo.current?.showModal();
  }

  async function publicar() {
    if (!puntuacion || enCurso.current) return;
    enCurso.current = true;
    setOcupado(true);
    setError(null);
    try {
      setCreada(await guardar<Valoracion>(ruta, 'POST', { puntuacion, comentario: comentario.trim() }));
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

  return <section aria-labelledby="valoracion-titulo" className="review-form">
    <h2 id="valoracion-titulo">Tu valoración</h2>
    {consulta.pendiente && <Cargando texto="Consultando valoración…" />}
    {consulta.error ? <Fallo error={consulta.error} reintentar={consulta.reintentar} enfocar={false} /> : null}
    {error && !valoracion ? error instanceof ErrorApi && error.status === 409
      ? <Aviso tono="warning"><p id={avisoId} role="alert">La valoración ya existe o el servicio cambió. Consulta de nuevo el detalle.</p></Aviso>
      : <Fallo error={error} id={avisoId} /> : null}
    {valoracion ? <div className="review-done">
      {creada && <div role="status"><Aviso tono="success"><p>Valoración publicada. ¡Gracias por compartir tu experiencia!</p></Aviso></div>}
      <p className="review-done__score"><Estrellas valor={valoracion.puntuacion} size={22} /><strong>{valoracion.puntuacion} de 5</strong>
        <span className="muted">· <Fecha valor={valoracion.createdAtUtc} /></span></p>
      {valoracion.comentario && <blockquote className="directory-description">{valoracion.comentario}</blockquote>}
      <p className="muted">Las valoraciones son definitivas y no se pueden editar.</p>
    </div> : <form hidden={consulta.pendiente || Boolean(consulta.error)} className="stack-form" onSubmit={preparar}
      aria-busy={ocupado} aria-describedby={error ? avisoId : undefined}>
      <fieldset disabled={ocupado}>
        <legend className="sr-only">¿Cómo fue el servicio?</legend>
        <p className="muted">¿Cómo fue el servicio? Tu opinión ayuda a otros clientes a elegir.</p>
        <SelectorEstrellas valor={puntuacion} cambiar={setPuntuacion} error={error} />
        <Campo etiqueta="Comentario" nombre="comentario" error={error} opcional contador={{ actual: comentario.length, maximo: 1000 }}>
          {control => <textarea {...control} rows={4} maxLength={1000} value={comentario} onChange={event => setComentario(event.target.value)} />}
        </Campo>
        <p className="muted field__hint">Tu nombre de pila, la puntuación y el comentario serán públicos.</p>
        <button className="btn" type="submit" disabled={!puntuacion}><Icon name="star" size={18} />Publicar valoración</button>
      </fieldset>
    </form>}
    <dialog ref={dialogo} className="dialog" aria-labelledby="valoracion-confirmar" aria-describedby="valoracion-confirmar-texto"
      onCancel={evento => { if (ocupado) evento.preventDefault(); }}>
      <h2 id="valoracion-confirmar">¿Publicar tu valoración?</h2>
      <p className="review-done__score"><Estrellas valor={puntuacion} size={22} /><strong>{puntuacion} de 5</strong></p>
      <p id="valoracion-confirmar-texto" className="muted">Se publicará una puntuación de {puntuacion} de 5. La opinión quedará guardada de forma definitiva.</p>
      <div className="dialog__actions">
        <button type="button" className="btn btn--ghost" autoFocus disabled={ocupado} onClick={() => dialogo.current?.close()}>Revisar opinión</button>
        <button type="button" className="btn" disabled={ocupado} onClick={publicar}>{ocupado ? 'Publicando…' : 'Confirmar publicación'}</button>
      </div>
    </dialog>
  </section>;
}

export function OpinionesPublicas({ tecnicoId }: { tecnicoId: string }) {
  const [page, setPage] = useState(1);
  const consulta = useConsulta<Pagina>('/tecnicos/' + encodeURIComponent(tecnicoId) + '/valoraciones?page=' + page + '&pageSize=10');
  const lista = consulta.datos;
  const totalPaginas = lista ? Math.ceil(lista.total / lista.pageSize) : 0;
  return <section className="reviews-public" aria-labelledby="opiniones-titulo" aria-busy={consulta.pendiente}>
    <h2 id="opiniones-titulo">Opiniones de clientes</h2>
    {consulta.pendiente && <Cargando texto="Cargando opiniones…" filas={2} />}
    {consulta.error ? <Fallo error={consulta.error} reintentar={consulta.reintentar} enfocar={false} /> : null}
    {lista && <>
      {lista.cantidad === 0 || lista.promedio === null
        ? <div className="empty-inline"><Icon name="star" /><p className="muted" role="status">Sin valoraciones todavía. Las opiniones aparecen cuando un cliente completa un servicio.</p></div>
        : <div className="reviews-summary" role="status">
          <strong className="reviews-summary__score">{promedioVisible(lista.promedio)}</strong>
          <div><Estrellas valor={lista.promedio} size={20} /><p className="muted">{plural(lista.cantidad, 'valoración', 'valoraciones')}</p></div>
        </div>}
      {lista.items.length === 0 && lista.total > 0 && <p>Esta página no tiene opiniones.</p>}
      <ul className="review-list">{lista.items.map(item => <li key={item.id}>
        <article className="review">
          <header className="review__head">
            <span className="avatar avatar--sm" aria-hidden="true">{item.nombreCliente.charAt(0).toUpperCase()}</span>
            <div><h3>{item.nombreCliente}</h3><p className="muted"><Fecha valor={item.createdAtUtc} /></p></div>
            <Estrellas valor={item.puntuacion} />
          </header>
          {item.comentario && <p className="directory-description">{item.comentario}</p>}
        </article>
      </li>)}</ul>
      {totalPaginas > 1 && <nav className="pagination" aria-label="Páginas de opiniones">
        {page > 1 && <button type="button" className="btn btn--ghost" onClick={() => setPage(page - 1)}><Icon name="back" size={18} />Anteriores</button>}
        <span className="pagination__current">Página {page} de {totalPaginas}</span>
        {page < totalPaginas && <button type="button" className="btn btn--ghost" onClick={() => setPage(page + 1)}>Más opiniones<Icon name="arrow" size={18} /></button>}
      </nav>}
    </>}
  </section>;
}
