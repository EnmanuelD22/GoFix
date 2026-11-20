export type Cuenta = {
  id: string;
  nombre: string;
  correo: string;
  telefono: string;
  rol: 'cliente' | 'tecnico';
  perfilTecnicoId: string | null;
};

type Problema = { title?: string; traceId?: string; errors?: Record<string, string[]> };

export class ErrorApi extends Error {
  constructor(public status: number, public problema: Problema) {
    super(problema.title ?? 'No se pudo completar la solicitud.');
  }
}

export async function solicitar<T>(ruta: string, opciones?: RequestInit): Promise<T> {
  const respuesta = await fetch(`/api/v1${ruta}`, {
    ...opciones, credentials: 'same-origin', cache: 'no-store',
  });
  if (!respuesta.ok) {
    const problema: Problema = await respuesta.json().catch(() => ({}));
    throw new ErrorApi(respuesta.status, problema ?? {});
  }
  if (respuesta.status === 204 || respuesta.headers.get('content-length') === '0') return undefined as T;
  if (!respuesta.headers.get('content-type')?.includes('json')) return undefined as T;
  return respuesta.json();
}

export function consultarCuenta(signal?: AbortSignal) {
  return solicitar<Cuenta>('/auth/me', { signal });
}

export async function guardar<T = void>(ruta: string, metodo: 'POST' | 'PUT', datos?: object) {
  // Un token de una sesión anterior no es válido después de entrar o salir.
  const { token } = await solicitar<{ token: string }>('/auth/csrf');
  return solicitar<T>(ruta, {
    method: metodo,
    headers: { 'X-CSRF-TOKEN': token, ...(datos ? { 'Content-Type': 'application/json' } : {}) },
    body: datos ? JSON.stringify(datos) : undefined,
  });
}
