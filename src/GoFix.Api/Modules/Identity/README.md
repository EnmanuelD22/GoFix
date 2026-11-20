# Identidad en la API

En `/api/v1`, este módulo expone el registro, inicio y cierre de sesión, consulta de sesión y edición de cuenta. Las mutaciones requieren el token de `GET /auth/csrf` en `X-CSRF-TOKEN`. El token debe solicitarse de nuevo cuando cambia el estado de autenticación.

El acceso se limita por IP en registro e inicio de sesión. La configuración del proxy de confianza se documenta en el README del repositorio.
