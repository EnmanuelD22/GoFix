import { promedioVisible } from '../lib/formato';
import { Icon } from './Icon';

/** Representación visual de una puntuación; el texto accesible lo aporta la etiqueta. */
export function Estrellas({ valor, size = 16 }: { valor: number; size?: number }) {
  const redondeado = Math.round(valor);
  return (
    <span className="stars" role="img" aria-label={`${promedioVisible(valor)} de 5 estrellas`}>
      {[1, 2, 3, 4, 5].map(posicion => (
        <Icon key={posicion} name="star" size={size} filled={posicion <= redondeado}
          className={posicion <= redondeado ? 'stars__on' : 'stars__off'} />
      ))}
    </span>
  );
}
