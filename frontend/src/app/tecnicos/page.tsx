import { Directorio } from '../../features/professionals/public-directory';

export default async function TecnicosPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const parametros = await searchParams;
  const consulta = new URLSearchParams();
  for (const clave of ['especialidadId', 'sectorId', 'page', 'pageSize']) {
    const valor = parametros[clave];
    if (typeof valor === 'string') consulta.set(clave, valor);
  }
  return <Directorio key={consulta.toString()} consulta={consulta.toString()} />;
}
