# Sugarista Cafe Digital Menu Theme

A mobile-first digital menu theme for a cafe QR page, built with HTML, CSS, Bootstrap, and Alpine.js. It now loads its content from the backend API instead of keeping the menu data hardcoded in the frontend.

## Stack

- `HTML5`
- `Bootstrap 5` via CDN
- `Alpine.js` via CDN
- Custom CSS in `assets/css/style.css`
- Menu data and interactions in `assets/js/app.js`

## Project Structure

```text
Sugarista-Theme/
├── index.html
├── assets/
│   ├── css/
│   │   └── style.css
│   └── js/
│       └── app.js
└── README.md
```

## How To Preview

Because this project now requests live API data, opening `index.html` directly from disk is not recommended for normal development. Use a local server or Docker so the API requests work correctly.

If you want a local server instead, run one of these from the project folder:

```bash
python3 -m http.server 8000
```

Then open `http://localhost:8000`.

## How To Edit The Menu

Most future content updates happen in the admin panel, not by manually editing the theme files.

The theme reads:

- menu data from `GET /api/menu`
- site copy and contact data from `GET /api/site-settings`

For local or production API routing, adjust:

- `assets/js/config.js`

## Branding Customization

Update these places first:

- the admin panel for menu items, categories, and public copy
- `assets/css/style.css` for colors, spacing, and card styling
- `assets/js/config.js` if the API base URL changes

Main design variables are at the top of `assets/css/style.css` under `:root`.

## Theme (Light / Dark)

The menu follows a light/dark theme that switches on the café's local clock, with a manual override.

- **Schedule settings** are managed in the admin panel under **تنظیمات سایت → تم و نمایش**
  (timezone id, light start hour, dark start hour, forced mode, allow manual override) and are served by `GET /api/site-settings`.
- **No flash on load**: an inline script in `<head>` runs before first paint and applies the saved manual
  choice or the default Tehran schedule (`dark 20–06`, `light 06–20`).
- **No reload on change**: `applyTheme()` swaps `data-theme` / `data-bs-theme` / `color-scheme` attributes only.
- **Manual override**: the floating button (top corner) saves the visitor's choice to
  `localStorage.sugarista_theme_pref`; the "خودکار" chip returns to the automatic schedule.
  Both are hidden when the café disables `allowManualThemeOverride`.
- A 60-second timer re-checks the schedule so the theme follows the café clock while the phone stays parked on one screen.
- Dark palette lives in the `:root[data-theme="dark"]` block at the bottom of `assets/css/style.css`.

## Deployment

This theme is designed to work best behind the included Docker Compose and nginx reverse proxy setup so it can reach the API at `/api`.

Recommended deployment:

- `docker compose up --build` from the workspace root
- public menu at `/`
- admin panel at `/admin/`

## Notes

- The layout is optimized for mobile QR visitors first.
- It also works on tablets and desktops.
- Bootstrap RTL is already included, so Persian layouts work naturally.
- The backend is now the source of truth for both menu content and site settings.
