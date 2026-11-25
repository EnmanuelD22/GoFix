"""Browser regression tests for RF-22 and RNF-06/07/12.

Run against the disposable database started by Test-Web.ps1. Requires Playwright.
GOFIX_WEB_BASE_URL must point at the HTTPS Next.js test server.
GOFIX_AXE_PATH optionally enables axe-core WCAG checks from a local axe.min.js.
"""
import argparse
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import time
from urllib.parse import urlparse
from uuid import uuid4

from playwright.sync_api import Error, expect, sync_playwright


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--browser", default="msedge")
    parser.add_argument("--executable")
    parser.add_argument("--smoke", action="store_true")
    parser.add_argument("--screenshots")
    args = parser.parse_args()
    root = os.environ["GOFIX_WEB_BASE_URL"].rstrip("/")
    if urlparse(root).scheme != "https":
        raise ValueError("GOFIX_WEB_BASE_URL debe usar HTTPS para las cookies seguras.")
    api = root + "/api/v1"
    password = "PruebaWeb" + uuid4().hex

    def call(context, method, path, data=None, expected=200, headers=None):
        options = {}
        if method != "GET":
            token = context.request.get(api + "/auth/csrf").json()["token"]
            options["headers"] = {"X-CSRF-TOKEN": token, **(headers or {})}
        if data is not None:
            options["data"] = data
        response = context.request.fetch(api + path, method=method, **options)
        assert response.status == expected, (method, path, response.status)
        return response.json() if response.body() else None

    def account(browser, role):
        context = browser.new_context(viewport={"width": 360, "height": 900})
        data = dict(nombre="Prueba Web " + role, correo=uuid4().hex + "@web.test",
                    telefono="+18095550100", contrasena=password, rol=role)
        call(context, "POST", "/auth/register", data, 201)
        info = call(context, "POST", "/auth/login",
                    dict(correo=data["correo"], contrasena=password))
        return context, info

    def navigate(page, path):
        page.goto(root + path, wait_until="networkidle")

    def audit(page, name):
        for width in (360, 768, 1440):
            page.set_viewport_size({"width": width, "height": 900})
            assert page.evaluate("document.documentElement.scrollWidth <= innerWidth"), (name, width)
        axe = os.environ.get("GOFIX_AXE_PATH")
        if axe:
            page.add_script_tag(path=axe)
            violations = page.evaluate("""async () => {
                const result = await axe.run(document, {
                    runOnly: {type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']}
                });
                return result.violations.map(item => ({
                    id: item.id, nodes: item.nodes.map(node => node.target)
                }));
            }""")
            assert not violations, (name, violations)
        page.set_viewport_size({"width": 360, "height": 900})
        if args.screenshots:
            Path(args.screenshots).mkdir(parents=True, exist_ok=True)
            page.screenshot(path=str(Path(args.screenshots) / (args.browser + "-" + name.replace(" ", "-") + ".png")), full_page=True)

    def capture_layouts(page, name):
        if not args.screenshots:
            return
        Path(args.screenshots).mkdir(parents=True, exist_ok=True)
        for label, width in (("mobile", 360), ("desktop", 1440)):
            page.set_viewport_size({"width": width, "height": 900})
            assert page.evaluate("document.documentElement.scrollWidth <= innerWidth"), (name, width)
            page.screenshot(path=str(Path(args.screenshots) /
                (args.browser + "-" + name + "-" + label + ".png")), full_page=True)
        page.set_viewport_size({"width": 360, "height": 900})

    def fields(page, title):
        page.locator("[name=especialidadId]").select_option(str(specialty))
        page.locator("[name=sectorId]").select_option(str(sector))
        page.locator("[name=titulo]").fill(title)
        page.locator("[name=descripcion]").fill("La instalación principal necesita una revisión y reparación.")
        page.locator("[name=direccion]").fill("Calle de Prueba 123")
        page.locator("[name=fechaPreferida]").fill(
            (datetime.now() + timedelta(days=3)).strftime("%Y-%m-%dT%H:%M"))

    with sync_playwright() as playwright:
        launch = {"headless": True}
        if args.executable:
            launch["executable_path"] = args.executable
        else:
            launch["channel"] = args.browser
        browser = playwright.chromium.launch(**launch)
        try:
            technician, tech_info = account(browser, "tecnico")
            customer, _ = account(browser, "cliente")
            specialty = call(technician, "GET", "/especialidades")[0]["id"]
            sector = call(technician, "GET", "/sectores")[0]["id"]
            profile = call(technician, "GET", "/tecnicos/me/perfil")
            call(technician, "PUT", "/tecnicos/me/perfil", dict(
                descripcion="Profesional con experiencia en instalaciones y mantenimiento de viviendas.",
                especialidadIds=[specialty], sectorIds=[sector], version=profile["version"]))
            technician_page = technician.new_page()
            navigate(technician_page, "/tecnico/perfil")
            expect(technician_page.locator("[name=descripcion]")).to_be_visible()
            audit(technician_page, "perfil técnico")
            technician_page.close()
            page = customer.new_page()
            public = browser.new_context()
            public_page = public.new_page()
            navigate(public_page, "/")
            expect(public_page.get_by_role("heading", level=1)).to_contain_text("a un clic de tu puerta")
            expect(public_page.locator(".search [name=especialidadId] option[value='" + str(specialty) + "']")).to_have_count(1)
            expect(public_page.locator(".specialty-grid a").first).to_be_visible()
            audit(public_page, "inicio")
            capture_layouts(public_page, "inicio")
            public_page.locator(".search [name=especialidadId]").select_option(str(specialty))
            public_page.locator(".search [name=sectorId]").select_option(str(sector))
            public_page.get_by_role("button", name="Buscar", exact=True).click()
            expect(public_page.get_by_role("heading", name="Encuentra un técnico")).to_be_visible()
            expect(public_page.locator("select[name=especialidadId]")).to_have_value(str(specialty))
            expect(public_page.locator("select[name=sectorId]")).to_have_value(str(sector))
            expect(public_page.locator(".tech-card").filter(has_text="Prueba Web tecnico")).to_be_visible()
            expect(public_page.locator(".tech-card a[href='/tecnicos/" + tech_info["perfilTecnicoId"] + "']").last).to_be_visible()
            audit(public_page, "directorio filtrado")
            capture_layouts(public_page, "directorio")
            navigate(public_page, "/registro")
            audit(public_page, "registro")
            public_page.keyboard.press("Tab")
            assert public_page.evaluate("document.activeElement !== document.body")
            # La cabecera y el logo contienen enlaces: Enter sobre el primer
            # foco navegaría fuera del registro. Verificamos el orden del formulario.
            public_page.locator("[name=rol]").focus()
            for field, value in {
                "nombre": "Registro por teclado", "telefono": "+18095550123",
                "correo": uuid4().hex + "@web.test", "contrasena": password
            }.items():
                public_page.keyboard.press("Tab")
                expect(public_page.locator("[name=" + field + "]")).to_be_focused()
                public_page.keyboard.insert_text(value)
            public_page.keyboard.press("Tab")
            expect(public_page.get_by_role("button", name="Crear cuenta", exact=True)).to_be_focused()
            public_page.keyboard.press("Enter")
            expect(public_page).to_have_url(root + "/acceso?registro=ok")
            expect(public_page.locator("[name=contrasena]")).to_have_value("")
            navigate(public_page, "/tecnicos")
            expect(public_page.get_by_role("heading", name="Encuentra un técnico")).to_be_visible()
            audit(public_page, "directorio")
            public_page.get_by_role("button", name="Buscar técnicos", exact=True).focus()
            public_page.keyboard.press("Enter")
            expect(public_page.get_by_role("heading", name="Encuentra un técnico")).to_be_visible()
            navigate(public_page, "/tecnicos?sectorId=2147483647")
            expect(public_page.get_by_role("heading", name="No encontramos técnicos con estos filtros")).to_be_visible()
            audit(public_page, "directorio vacío")
            creation = "/cliente/solicitudes/nueva?tecnico=" + tech_info["perfilTecnicoId"]
            navigate(page, creation)
            title = "Reparación de instalación doméstica"
            fields(page, title)
            audit(page, "nueva solicitud")
            page.route("**/api/v1/solicitudes", lambda route: route.fulfill(
                status=400, content_type="application/problem+json",
                body=json.dumps({"status": 400, "errors": {"titulo": ["Revisa el título de la visita."]}})), times=1)
            page.get_by_role("button", name="Enviar solicitud", exact=True).click()
            expect(page.locator("[name=titulo]")).to_have_attribute("aria-invalid", "true")
            assert page.locator("[name=titulo]").evaluate("""field => {
                const ids = (field.getAttribute("aria-describedby") || "").split(" ");
                return ids.some(id => document.getElementById(id)?.textContent.includes("Revisa el título"));
            }""")
            expect(page.locator("[name=titulo]")).to_have_value(title)
            audit(page, "validación asociada")

            attempts = []
            original_id = None

            def intercept_creation(route):
                nonlocal original_id
                if route.request.method != "POST":
                    route.continue_()
                    return
                attempts.append(route.request.headers["idempotency-key"])
                if len(attempts) == 1:
                    route.fulfill(status=503, content_type="application/problem+json",
                                  body=json.dumps({"status": 503, "title": "Fallo recuperable",
                                                   "traceId": "prueba-web", "code": "http_503"}))
                elif len(attempts) == 2:
                    response = route.fetch()
                    assert response.status == 201
                    original_id = response.json()["id"]
                    route.abort("failed")
                else:
                    route.continue_()

            if not args.smoke:
                page.route("**/api/v1/solicitudes", intercept_creation)
                with page.expect_response(lambda response: response.url.endswith("/api/v1/solicitudes") and response.status == 503):
                    page.locator("form.requests-form").evaluate(
                        "form => { form.requestSubmit(); form.requestSubmit(); }")
                expect(page.locator(".identity-notice")).to_be_visible()
                expect(page.locator("[name=titulo]")).to_have_value(title)
                assert len(attempts) == 1, "El doble envío produjo más de una petición."
                with page.expect_event("requestfailed", predicate=lambda request: request.method == "POST" and request.url.endswith("/api/v1/solicitudes")):
                    page.get_by_role("button", name="Enviar solicitud", exact=True).click()
                expect(page.locator(".identity-notice")).to_be_visible()
                expect(page.locator("[name=titulo]")).to_have_value(title)
                assert original_id is not None, "La prueba de respuesta perdida debe confirmar el commit."
                assert call(customer, "GET", "/solicitudes")["total"] == 1
            page.get_by_role("button", name="Enviar solicitud", exact=True).click()
            expect(page.get_by_role("heading", name=title, exact=True)).to_be_visible(timeout=15000)
            request_id = page.url.rsplit("/", 1)[1]
            if not args.smoke:
                assert request_id == original_id
                assert len(attempts) == 3 and len(set(attempts)) == 1
                assert call(customer, "GET", "/solicitudes")["total"] == 1
            page.unroute("**/api/v1/solicitudes")
            audit(page, "detalle")
            delete = page.get_by_role("button", name="Eliminar solicitud", exact=True)
            delete.focus()
            page.keyboard.press("Enter")
            dialog = page.get_by_role("dialog")
            expect(dialog).to_be_visible()
            assert dialog.evaluate("dialog => dialog.contains(document.activeElement)")
            page.keyboard.press("Escape")
            expect(dialog).not_to_be_visible()
            expect(delete).to_be_focused()

            if not args.smoke:
                page.get_by_role("link", name="Editar solicitud", exact=True).click()
                expect(page.locator("[name=titulo]")).to_have_value(title)
                item = call(customer, "GET", "/solicitudes/" + request_id)
                remote_title = "Cambio confirmado desde otra pestaña"
                call(customer, "PUT", "/solicitudes/" + request_id, dict(
                    titulo=remote_title, descripcion=item["descripcion"], direccion=item["direccion"],
                    sectorId=item["sectorId"], fechaPreferida=item["fechaPreferida"], version=item["version"]))
                local_title = "Edición local que debe conservarse"
                page.locator("[name=titulo]").fill(local_title)
                mutations = []
                page.on("request", lambda request: mutations.append(request) if
                        request.method == "PUT" and request.url.endswith("/solicitudes/" + request_id) else None)
                page.get_by_role("button", name="Guardar cambios", exact=True).click()
                expect(page.get_by_role("button", name="Cargar la versión actual", exact=True)).to_be_visible()
                expect(page.locator("[name=titulo]")).to_have_value(local_title)
                page.wait_for_timeout(400)
                assert len(mutations) == 1, "Un conflicto no debe reintentar la mutación."
                page.get_by_role("button", name="Cargar la versión actual", exact=True).click()
                expect(page.locator("[name=titulo]")).to_have_value(remote_title)
                navigate(page, creation)
                fields(page, "Solicitud con espera agotada")

                def delay_creation(route):
                    if route.request.method != "POST":
                        route.continue_()
                        return
                    time.sleep(31)
                    try:
                        route.abort("timedout")
                    except Error:
                        pass

                page.route("**/api/v1/solicitudes", delay_creation)
                page.get_by_role("button", name="Enviar solicitud", exact=True).click()
                expect(page.locator(".identity-notice")).to_be_visible(timeout=40000)
                expect(page.locator("[name=titulo]")).to_have_value("Solicitud con espera agotada")
                assert call(customer, "GET", "/solicitudes")["total"] == 1
                page.unroute("**/api/v1/solicitudes")
                if args.screenshots:
                    Path(args.screenshots).mkdir(parents=True, exist_ok=True)
                    page.screenshot(path=str(Path(args.screenshots) / "timeout-form.png"), full_page=True)

            assert page.evaluate("""password => {
                const values = [...Object.values(localStorage), ...Object.values(sessionStorage)];
                return values.every(value => !value.includes(password));
            }""", password), "Las credenciales no deben persistirse en almacenamiento web."
            # La agenda usa la solicitud real creada por el mismo recorrido.
            item = call(technician, "GET", "/solicitudes/" + request_id)
            panel_page = technician.new_page()
            navigate(panel_page, "/tecnico/panel")
            expect(panel_page.get_by_role("heading", name="Mi agenda de servicios")).to_be_visible()
            expect(panel_page.get_by_role("link", name="1 solicitudes pendientes", exact=True)).to_be_visible()
            expect(panel_page.get_by_role("link", name="0 solicitudes aceptadas", exact=True)).to_be_visible()
            expect(panel_page.get_by_role("link", name=item["titulo"], exact=True)).to_be_visible()
            panel_page.get_by_role("button", name="Aceptar solicitud", exact=True).click()
            action_dialog = panel_page.get_by_role("dialog", name="Aceptar solicitud", exact=True)
            expect(action_dialog).to_be_visible()
            action_dialog.locator("[name=fechaConfirmada]").fill(
                (datetime.now() + timedelta(days=2)).strftime("%Y-%m-%dT%H:%M"))
            action_dialog.get_by_role("button", name="Confirmar", exact=True).click()
            expect(action_dialog).not_to_be_visible()
            expect(panel_page.get_by_role("link", name="0 solicitudes pendientes", exact=True)).to_be_visible()
            expect(panel_page.get_by_role("link", name="1 solicitudes aceptadas", exact=True)).to_be_visible()
            expect(panel_page.get_by_role("region", name="Historial de la solicitud")).to_contain_text("Aceptada")
            visit_link = panel_page.locator(".data-table").get_by_role("button", name=item["titulo"], exact=True)
            expect(visit_link).to_be_visible()
            visit_link.click()
            expect(visit_link).to_have_attribute("aria-pressed", "true")
            expect(panel_page.get_by_role("heading", name="Detalle de solicitud", exact=True)).to_be_visible()
            audit(panel_page, "panel técnico")
            capture_layouts(panel_page, "panel-tecnico")
            panel_page.get_by_role("link", name=item["titulo"], exact=True).click()
            expect(panel_page.get_by_role("heading", name=item["titulo"], exact=True)).to_be_visible()
            expect(panel_page.get_by_role("button", name="Iniciar servicio", exact=True)).to_be_visible()
            navigate(panel_page, "/tecnico/panel")
            panel_page.get_by_role("button", name="Cerrar sesión", exact=True).click()
            expect(panel_page).to_have_url(root + "/acceso")
            expect(panel_page.locator("[name=contrasena]")).to_have_value("")
            assert technician.request.get(api + "/auth/me").status == 401
            panel_page.close()
            customer.clear_cookies()
            navigate(page, "/cliente/solicitudes")
            expect(page).to_have_url(root + "/acceso?sesion=expirada")
            expect(page.locator("[name=contrasena]")).to_have_value("")
            audit(page, "sesión vencida")
            print("PASS", args.browser, "accesibilidad, teclado, adaptación y flujo de solicitud",
                  "con recuperación, idempotencia, timeout y conflicto" if not args.smoke else "")
        finally:
            browser.close()


if __name__ == "__main__":
    main()
