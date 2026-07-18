# Sugarista

## Apply UI changes with Docker

The UI containers now mount your local source files, so changes are picked up correctly when you restart the stack.

### Recommended command (after editing UI files)

```bash
make up
```

Or:

```bash
./scripts/dev-up.sh
```

Both run:

```bash
docker compose up -d --build --force-recreate admin-ui public-ui proxy
```

`--force-recreate` is required for the **admin panel** so the container re-runs its build step.  
`--build` alone may leave old containers running.

### What happens now

| App | Folder | How changes apply |
|-----|--------|-------------------|
| Digital menu | `Sugarista-Theme/` | **Immediate** — files are volume-mounted; refresh the browser |
| Admin panel | `Sugarista-Admin/` | **Auto-rebuild** when you save files (file watcher in container). First load still happens on container start. |

### URLs

- Digital menu: http://localhost/
- Admin panel: http://localhost/admin/

### Full stack

```bash
docker compose up -d --build --force-recreate
```

### Admin-only quick refresh

If auto-rebuild is disabled or you want to force a rebuild immediately:

```bash
make admin-reload
# or
docker compose restart admin-ui
```

Then hard-refresh the browser (`Cmd+Shift+R` on Mac, `Ctrl+Shift+R` on Windows/Linux).

### Local dev without Docker (optional)

**Admin:**

```bash
cd Sugarista-Admin && npm install && npm run dev
```

Open http://localhost:5173/admin/

**Theme:** use volume mounts via Docker, or serve `Sugarista-Theme` with any static file server.
