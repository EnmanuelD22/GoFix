import { Icon, type IconName } from '../../components/Icon';

export const estados = {
  Pendiente: 'Pendiente', Aceptada: 'Aceptada', EnProceso: 'En proceso',
  Completada: 'Completada', Rechazada: 'Rechazada', Cancelada: 'Cancelada',
} as const;

export type Estado = keyof typeof estados;

const tonos: Record<Estado, string> = {
  Pendiente: 'warning', Aceptada: 'info', EnProceso: 'accent',
  Completada: 'success', Rechazada: 'error', Cancelada: 'neutral',
};

export const iconosEstado: Record<Estado, IconName> = {
  Pendiente: 'clock', Aceptada: 'cal', EnProceso: 'play',
  Completada: 'check', Rechazada: 'close', Cancelada: 'close',
};

export function etiquetaEstado(valor: string) {
  return estados[valor as Estado] ?? valor;
}

export function EstadoTag({ estado }: { estado: string }) {
  const conocido = estado in estados ? estado as Estado : undefined;
  return <span className={'tag tag--' + (conocido ? tonos[conocido] : 'neutral') + ' tag--status'}>
    {conocido && <Icon name={iconosEstado[conocido]} size={14} />}
    {etiquetaEstado(estado)}
  </span>;
}

const pasos: Estado[] = ['Pendiente', 'Aceptada', 'EnProceso', 'Completada'];

/** Progreso del servicio. Rechazada y Cancelada terminan el recorrido antes de completarse. */
export function ProgresoSolicitud({ estado }: { estado: Estado }) {
  const terminado = estado === 'Rechazada' || estado === 'Cancelada';
  const actual = terminado ? -1 : pasos.indexOf(estado);
  return <ol className={'stepper' + (terminado ? ' stepper--stopped' : '')} aria-label="Progreso del servicio">
    {pasos.map((paso, indice) => {
      const situacion = indice < actual || estado === 'Completada' ? 'done' : indice === actual ? 'current' : 'todo';
      return <li key={paso} className={'stepper__step is-' + situacion} aria-current={situacion === 'current' ? 'step' : undefined}>
        <span className="stepper__dot" aria-hidden="true">{situacion === 'done' ? <Icon name="check" size={14} /> : indice + 1}</span>
        <span className="stepper__label">{estados[paso]}<span className="sr-only">{situacion === 'done' ? ', completado' : situacion === 'current' ? ', estado actual' : ''}</span></span>
      </li>;
    })}
  </ol>;
}
