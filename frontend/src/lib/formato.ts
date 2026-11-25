const LOCALE = 'es-DO';

const fechaHora = new Intl.DateTimeFormat(LOCALE, { dateStyle: 'medium', timeStyle: 'short' });
const fecha = new Intl.DateTimeFormat(LOCALE, { dateStyle: 'medium' });
const decimal = new Intl.NumberFormat(LOCALE, { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const entero = new Intl.NumberFormat(LOCALE);

export function fechaVisible(valor: string) {
  return fechaHora.format(new Date(valor));
}

export function fechaCorta(valor: string) {
  return fecha.format(new Date(valor));
}

export function promedioVisible(valor: number) {
  return decimal.format(valor);
}

/** Devuelve «1 solicitud» o «3 solicitudes» con el número formateado. */
export function plural(cantidad: number, singular: string, varios: string) {
  return `${entero.format(cantidad)} ${cantidad === 1 ? singular : varios}`;
}

export function iniciales(nombre: string) {
  return nombre.trim().split(/\s+/).slice(0, 2).map(parte => parte[0]?.toUpperCase() ?? '').join('');
}

/** Valor para un campo datetime-local en la zona horaria del navegador. */
export function valorFechaLocal(valor: Date | string) {
  const local = new Date(valor);
  return new Date(local.getTime() - local.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}
