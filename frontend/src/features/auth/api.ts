import { rutaAcceso } from '../../lib/navegacion';

export type Cuenta = {
  id: string;
  nombre: string;
  correo: string;
  telefono: string;
  rol: 'cliente' | 'tecnico';
  perfilTecnicoId: string | null;
};

type Problema = { code?: string; title?: string; traceId?: string; errors?: Record<string, string[]> };

export class ErrorApi extends Error {
  constructor(public status: number, public problema: Problema) {
    super(problema.title ?? 'No se pudo completar la solicitud.');
  }
}

export async function solicitar<T>(ruta: string, opciones?: RequestInit): Promise<T> {
  const limite = AbortSignal.timeout(30_000);
  const signal = opciones?.signal ? AbortSignal.any([opciones.signal, limite]) : limite;
  try {
    const respuesta = await fetch('/api/v1' + ruta, {
      ...opciones, signal, credentials: 'same-origin', cache: 'no-store',
    });
    if (!respuesta.ok) {
      if (respuesta.status === 401 && !['/auth/login', '/auth/register'].includes(ruta.split('?')[0])
        && typeof window !== 'undefined' && window.location.pathname !== '/acceso') {
        // Se conserva la ruta actual para volver a ella después de iniciar sesión.
        window.location.replace(rutaAcceso(window.location.pathname + window.location.search, true));
      }
      const problema: Problema = await respuesta.json().catch((fallo: unknown) => {
        if (signal.aborted) throw fallo;
        return {};
      });
      throw new ErrorApi(respuesta.status, problema ?? {});
    }
    if (respuesta.status === 204 || respuesta.headers.get('content-length') === '0') return undefined as T;
    if (!respuesta.headers.get('content-type')?.includes('json')) return undefined as T;
    return await respuesta.json();
  } catch (fallo) {
    if (opciones?.signal?.aborted) throw fallo;
    if (limite.aborted) {
      throw new ErrorApi(408, { title: 'La operación tardó demasiado. Revisa su resultado antes de volver a enviarla.' });
    }
    if (fallo instanceof ErrorApi) throw fallo;
    throw new ErrorApi(0, { title: 'No pudimos conectar. Comprueba tu conexión y vuelve a intentarlo.' });
  }
}

export function consultarCuenta(signal?: AbortSignal) {
  return solicitar<Cuenta>('/auth/me', { signal });
}

export async function guardar<T = void>(ruta: string, metodo: 'POST' | 'PUT' | 'DELETE', datos?: object, encabezados?: Record<string, string>) {
  // Un token de una sesión anterior no es válido después de entrar o salir.
  const { token } = await solicitar<{ token: string }>('/auth/csrf');
  return solicitar<T>(ruta, {
    method: metodo,
    headers: { ...encabezados, 'X-CSRF-TOKEN': token, ...(datos ? { 'Content-Type': 'application/json' } : {}) },
    body: datos ? JSON.stringify(datos) : undefined,
  });
}
