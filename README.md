# ReactLib

[![visitas](https://hits.sh/reactlib.lostdou.dev.svg?style=flat-square&label=visitas)](https://hits.sh/reactlib.lostdou.dev/)

**[reactlib.lostdou.dev](https://reactlib.lostdou.dev)**

Catálogo buscable de videos de reacción de X. Copiás el link de un video, lo pegás en el texto de
tu post y **X adjunta ese video ya subido**, con la atribución nativa al autor en el reproductor,
sin re-subir nada y sin citar.

El truco está en el formato de la URL:

```
https://x.com/<usuario>/status/<tweetId>/video/<n>
```

No alojamos media: la base guarda solo URLs y metadata, y las imágenes y videos salen del CDN de X.

Cualquiera puede publicar sin registrarse. Los tags son la única compuerta: los que ya existen se
aplican solos, los nuevos quedan propuestos hasta que se aprueban, así la taxonomía no se llena de
sinónimos y la búsqueda sigue sirviendo.

## Stack

| Capa | Tecnología | Dónde vive |
|---|---|---|
| Base | SQL Server | MonsterASP (plan gratuito, 1 GB) |
| API | .NET 10, Minimal API + Dapper | MonsterASP (256 MB RAM) |
| Front | Angular 22 standalone, CSS puro | GitHub Pages |

Las restricciones del plan gratuito son lo que explica casi todas las decisiones de diseño:
paginación keyset en vez de `OFFSET`, índices filtrados y covering, hash perceptual calculado en
el browser, y nada de tablas con una fila por acción de usuario.

## Licencia

MIT: podés forkearlo, modificarlo y usarlo, incluso comercialmente, manteniendo el aviso de
copyright. Ver [LICENSE](LICENSE).
