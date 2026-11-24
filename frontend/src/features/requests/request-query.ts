export async function consultaSolicitudes(parametros: Promise<Record<string, string | string[] | undefined>>) {
  const valores = await parametros;
  const consulta = new URLSearchParams();
  for (const nombre of ['estado', 'page', 'pageSize']) {
    const valor = valores[nombre];
    if (typeof valor === 'string' && valor) consulta.set(nombre, valor);
  }
  return consulta.toString();
}
