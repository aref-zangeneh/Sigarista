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
