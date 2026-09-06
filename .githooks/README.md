# Git hooks

Hooks versionados del repo. Git **no** los activa solo: cada clon tiene que
apuntar su `core.hooksPath` una vez.

```bash
git config core.hooksPath .githooks
```

## `pre-commit`

Corre Prettier sobre los archivos staged de `frontend/web` y los re-stagea si
los reformateó, para que `npm run format:check` del CI no sea el primero en
avisar.

- Si el archivo tiene cambios **sin stagear**, no lo modifica: sólo verifica y
  aborta el commit con instrucciones. Así nunca se cuela al commit trabajo que
  no revisaste.
- Usa `npx --no-install`, así que necesita `npm ci` hecho en `frontend/web`.
- Para saltearlo puntualmente: `git commit --no-verify`.
