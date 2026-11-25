"""Ensayo HTTP reproducible; usar solo con una API conectada a una base desechable."""
import argparse
import concurrent.futures
import json
import os
import platform
import ssl
import time
import urllib.error
import urllib.request
import http.cookiejar
import uuid
from datetime import datetime, timezone


def percentile(values, fraction):
    if not values:
        return None
    ordered = sorted(values)
    return round(ordered[min(len(ordered) - 1, int((len(ordered) - 1) * fraction))], 2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-url', required=True, help='Origen HTTP de la API de pruebas')
    parser.add_argument('--duration', type=int, default=600, help='Duración en segundos')
    parser.add_argument('--workers', type=int, default=20)
    parser.add_argument('--min-requests', type=int, default=10000)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    if args.duration < 1 or args.workers < 1 or args.min_requests < 1:
        parser.error('duration, workers y min-requests deben ser positivos')
    origin = args.base_url.rstrip('/')
    if not origin.startswith(('http://127.0.0.1:', 'https://127.0.0.1:', 'http://localhost:', 'https://localhost:')):
        parser.error('Solo se admiten servicios locales de prueba')
    paths = ('/api/v1/tecnicos?page=1&pageSize=20',
             '/api/v1/tecnicos?page=20&pageSize=20',
             '/api/v1/tecnicos?page=50&pageSize=20',
             '/api/v1/especialidades', '/api/v1/sectores')
    context = ssl._create_unverified_context() if origin.startswith('https://') else None
    def open_request(request):
        return urllib.request.urlopen(request, timeout=15, context=context)
    # Calentar la aplicación antes del cronómetro y exigir un directorio sembrado.
    with open_request(origin + paths[0]) as response:
        payload = json.load(response)
    if payload.get('total') != 1000:
        raise RuntimeError('Se requieren exactamente 1000 técnicos publicados en la base aislada')
    technician = payload['items'][0]['id']
    jar = http.cookiejar.CookieJar()
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar),
        urllib.request.HTTPSHandler(context=context) if context else urllib.request.HTTPHandler())
    def session_request(path, method='GET', data=None, token=None):
        headers = {'Content-Type': 'application/json'}
        if token:
            headers['X-CSRF-TOKEN'] = token
        request = urllib.request.Request(origin + path, data=json.dumps(data).encode() if data is not None else None,
                                         headers=headers, method=method)
        with opener.open(request, timeout=15) as response:
            body = response.read()
            return json.loads(body) if body else None
    email = f'load-{uuid.uuid4().hex}@perf.gofix.test'
    password = 'ClaveDePruebas2026'
    token = session_request('/api/v1/auth/csrf')['token']
    session_request('/api/v1/auth/register', 'POST',
        {'nombre': 'Cliente de carga', 'correo': email, 'telefono': '+59176543210',
         'contrasena': password, 'rol': 'cliente'}, token)
    token = session_request('/api/v1/auth/csrf')['token']
    session_request('/api/v1/auth/login', 'POST', {'correo': email, 'contrasena': password}, token)
    token = session_request('/api/v1/auth/csrf')['token']
    cookie = '; '.join(f'{item.name}={item.value}' for item in jar)
    if not cookie:
        raise RuntimeError('No se recibió cookie de sesión')

    start = time.monotonic()
    deadline = start + args.duration
    samples = []
    def worker(index):
        local = []
        count = 0
        while time.monotonic() < deadline:
            write = (index + count) % 5 == 0
            path = '/api/v1/solicitudes' if write else paths[(index + count) % len(paths)]
            began = time.perf_counter()
            try:
                if write:
                    body = {'tecnicoId': technician, 'especialidadId': 1, 'sectorId': 1,
                            'titulo': 'Revisar instalación',
                            'descripcion': 'El interruptor de la sala necesita una revisión y mantenimiento.',
                            'direccion': 'Calle de prueba, número 14',
                            'fechaPreferida': datetime.fromtimestamp(time.time() + 86400 * 5, timezone.utc).isoformat()}
                    request = urllib.request.Request(origin + path, data=json.dumps(body).encode(),
                        headers={'Content-Type': 'application/json', 'Cookie': cookie,
                                 'X-CSRF-TOKEN': token, 'Idempotency-Key': str(uuid.uuid4())}, method='POST')
                else:
                    request = origin + path
                with open_request(request) as response:
                    response.read()
                    status = response.status
            except urllib.error.HTTPError as error:
                status = error.code
            except (OSError, TimeoutError):
                status = 0
            local.append(('write' if write else 'read', round((time.perf_counter() - began) * 1000, 3), status))
            count += 1
        return local
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        for result in pool.map(worker, range(args.workers)):
            samples.extend(result)
    elapsed = time.monotonic() - start
    def result_for(kind, expected):
        subset = [(milliseconds, status) for group, milliseconds, status in samples if group == kind]
        errors = sum(status != expected for _, status in subset)
        return {'count': len(subset), 'p50Ms': percentile([value for value, _ in subset], .50),
                'p95Ms': percentile([value for value, _ in subset], .95),
                'p99Ms': percentile([value for value, _ in subset], .99), 'errors': errors,
                'errorRate': round(errors / len(subset), 6) if subset else 1}
    reads = result_for('read', 200)
    writes = result_for('write', 201)
    report = {
        'timestampUtc': datetime.now(timezone.utc).isoformat(),
        'scenario': {'durationSeconds': round(elapsed, 2), 'concurrentUsers': args.workers,
                     'publishedTechnicians': 1000, 'requests': len(samples), 'operations': list(paths)},
        'host': {'system': platform.platform(), 'processor': platform.processor(),
                 'logicalCpus': os.cpu_count()},
        'read': reads, 'write': writes,
    }
    report['passed'] = (elapsed >= args.duration and args.workers >= 20 and len(samples) >= args.min_requests
                        and reads['p95Ms'] <= 800 and writes['p95Ms'] <= 1200
                        and (reads['errors'] + writes['errors']) / len(samples) < .01)
    with open(args.output, 'w', encoding='utf-8') as target:
        json.dump(report, target, ensure_ascii=False, indent=2)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    raise SystemExit(0 if report['passed'] else 1)


if __name__ == '__main__':
    main()
