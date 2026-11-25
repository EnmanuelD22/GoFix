import type { Cuenta } from '../features/auth/api';

export function inicioPorRol(rol: Cuenta['rol']) {
  return rol === 'tecnico' ? '/tecnico/panel' : '/cliente/panel';
}

export function solicitudesPorRol(rol: Cuenta['rol']) {
  return rol === 'tecnico' ? '/tecnico/solicitudes' : '/cliente/solicitudes';
}

/** Acepta solo rutas internas para evitar redirecciones abiertas tras el acceso. */
export function destinoSeguro(valor: string | null | undefined) {
  if (!valor || !valor.startsWith('/') || valor.startsWith('//') || valor.includes('\\')) return null;
  if (/^\/(acceso|registro)(\/|\?|$)/.test(valor)) return null;
  return valor;
}

export function rutaAcceso(volver?: string, sesionExpirada = false) {
  const consulta = new URLSearchParams();
  if (sesionExpirada) consulta.set('sesion', 'expirada');
  const destino = destinoSeguro(volver);
  if (destino) consulta.set('volver', destino);
  const texto = consulta.toString();
  return texto ? '/acceso?' + texto : '/acceso';
}
