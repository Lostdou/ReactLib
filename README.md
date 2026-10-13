# Twitter Reaction Library

[![visitas](https://hits.sh/reactlib.lostdou.dev.svg?style=flat-square&label=visitas)](https://hits.sh/reactlib.lostdou.dev/)

Catálogo buscable de videos de reacción de X. Copiás el link de un video, lo pegás en el texto de
tu post y **X adjunta ese video ya subido**, con la atribución nativa al autor en el reproductor,
sin re-subir nada y sin citar.

El truco es el formato de la URL:

```
https://x.com/<usuario>/status/<tweetId>/video/<n>
```

No alojamos media: la base guarda solo URLs y metadata, y las imágenes y videos salen del CDN de X.

## Stack

| Capa | Tecnología | Dónde vive |
|---|---|---|
| Base | SQL Server 2025 | MonsterASP (plan gratuito, 1 GB) |
| API | .NET 10, Minimal API + Dapper | MonsterASP (256 MB RAM) |
| Front | Angular 22 standalone, CSS puro | GitHub Pages |

## Correr local

```bash
# 1. base
sqlcmd -S localhost -E -C -Q "IF DB_ID('reactlib') IS NULL CREATE DATABASE reactlib;"
sqlcmd -S localhost -E -C -d reactlib -i API/db/001_schema.sql
sqlcmd -S localhost -E -C -d reactlib -i API/db/002_publish.sql
sqlcmd -S localhost -E -C -d reactlib -i API/db/dev_seed.sql   # datos falsos, solo local

# 2. API en http://localhost:5080
dotnet run --project API/src/ReactLib.Api

# 3. front en http://localhost:4200
cd Web && npm start
```

Para cargar videos reales, con la API levantada y `Api__AdminKey` seteada:

```bash
curl -X POST http://localhost:5080/api/admin/videos \
  -H "X-Admin-Key: $ADMIN_KEY" \
  -H "Content-Type: application/json" \
  -d '{"url":"https://x.com/usuario/status/123","tags":"gato, indiferencia"}'
```

Los scripts de `API/db/` son idempotentes y están numerados. `dev_seed.sql` queda fuera de esa
numeración a propósito: los TweetId son inventados y no resuelven a nada.

> Para correr SQL a mano contra estas tablas hace falta `SET QUOTED_IDENTIFIER ON`, porque casi
> todos los índices son filtrados. SqlClient ya lo manda; sqlcmd no.

> El dHash de la miniatura lo calcula **el browser** (`Web/src/app/core/dhash.ts`) y viaja como
> string en el campo `dhash`: así la caja de 256 MB nunca decodifica una imagen, y evitamos a
> ImageSharp, que es vulnerable hasta la 2.x y pide clave de licencia de la 3.x en adelante. Si el
> hash no viene, la fila simplemente queda fuera del dedup perceptual.

> `video.twimg.com` devuelve **403 si le llega un `Referer`**. Por eso `index.html` declara
> `<meta name="referrer" content="no-referrer">`: sin eso no se reproduce ningún video. El CDN de
> imágenes (`pbs.twimg.com`) no tiene esa restricción.

## Deploy

Dos workflows independientes, cada uno disparado por cambios en su carpeta:

- `.github/workflows/web.yml` → GitHub Pages. Calcula el `base-href` solo y copia `index.html` a
  `404.html` para que anden los deep links.
- `.github/workflows/api.yml` → MonsterASP vía WebDeploy. Escribe `appsettings.Production.json`
  desde los secrets **recién en el artefacto publicado**, así las credenciales nunca existen en el
  repo ni en su historial.

Secrets que hay que cargar en el repo (Settings → Secrets and variables → Actions):

| Secret | De dónde sale |
|---|---|
| `MONSTERASP_WEBSITE_NAME` | `msdeploySite` del publish profile |
| `MONSTERASP_SERVER` | `publishUrl` del publish profile |
| `MONSTERASP_USERNAME` | `userName` del publish profile |
| `MONSTERASP_PASSWORD` | `userPWD` del publish profile |
| `RL_CONNECTION_STRING` | panel de MonsterASP → Databases |
| `RL_ADMIN_KEY` | lo generás vos: `python -c "import secrets; print(secrets.token_urlsafe(32))"` |

El publish profile (`API/WebDeploy.publishSettings`) está en `.gitignore` y **no debe commitearse**.

### Dos cosas que hay que hacer a mano en el panel

1. **Activar HTTPS**: Domains → candado verde → Let's Encrypt → Enable HTTPS. Sin esto el front en
   Pages (que es https) no puede llamar a la API por mixed content. En el plan gratuito **el
   certificado se renueva a mano cada 90 días**.
2. **GitHub Pages necesita el repo público** si la cuenta es Free: publicar Pages desde un repo
   privado requiere plan pago.

## Estado

| Fase | Qué incluye | Estado |
|---|---|---|
| 0 | Esqueleto, esquema, health, CORS, workflows | listo |
| 1 | Catálogo read-only: grilla, detalle, botón copiar | listo |
| 2 | Metadata vía fxtwitter/vxtwitter + dedup en 3 capas | listo |
| 3 | Ingesta pública con moderación | pendiente |
| 4 | Tags, búsqueda y autocomplete | parcial (falta autocomplete) |
| 5 | Output cache en disco, ETag, contador de copiados | pendiente |
| 6 | Link rot, decay, prerender, backup | pendiente |
