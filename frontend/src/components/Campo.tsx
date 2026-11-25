'use client';

import { useId, type ReactNode } from 'react';
import { erroresDeCampo } from './Fallo';
import { Icon } from './Icon';

export type ControlProps = {
  id: string;
  name: string;
  'aria-invalid'?: true;
  'aria-describedby'?: string;
};

/**
 * Campo de formulario con etiqueta, ayuda, contador y error asociados mediante aria-describedby.
 * El control se recibe como función para conservar inputs, selects o textareas nativos.
 */
export function Campo({ etiqueta, nombre, error, ayuda, contador, opcional = false, className, children }: {
  etiqueta: ReactNode;
  nombre: string;
  error?: unknown;
  ayuda?: ReactNode;
  contador?: { actual: number; maximo: number };
  opcional?: boolean;
  className?: string;
  children: (control: ControlProps) => ReactNode;
}) {
  const id = useId();
  const mensajes = erroresDeCampo(error)[nombre];
  const ayudaId = ayuda ? id + '-ayuda' : undefined;
  const contadorId = contador ? id + '-contador' : undefined;
  const errorId = mensajes?.length ? id + '-error' : undefined;
  const descripcion = [errorId, ayudaId, contadorId].filter(Boolean).join(' ') || undefined;
  return (
    <div className={'field' + (className ? ' ' + className : '')}>
      <label htmlFor={id}>{etiqueta}{opcional && <span className="field__optional"> (opcional)</span>}</label>
      {children({ id, name: nombre, 'aria-invalid': errorId ? true : undefined, 'aria-describedby': descripcion })}
      {(ayuda || contador) && <div className="field__meta">
        {ayuda && <small id={ayudaId} className="field__hint">{ayuda}</small>}
        {contador && <small id={contadorId} className={'field__count' + (contador.actual > contador.maximo ? ' is-over' : '')}>
          {contador.actual.toLocaleString('es-DO')} de {contador.maximo.toLocaleString('es-DO')} caracteres
        </small>}
      </div>}
      {errorId && <p id={errorId} className="field__error"><Icon name="alert" size={16} />{mensajes!.join(' ')}</p>}
    </div>
  );
}
