# Sugarista Admin

The Sugarista admin app is a React + Vite dashboard for managing:

- menu categories
- menu items
- public site settings

The UI is configured for Persian and right-to-left layout.

## Development

Install dependencies:

```bash
npm install
```

Run the dev server:

```bash
npm run dev
```

The Vite dev server proxies `/api` requests to `http://localhost:5282`, so start the backend API first.

## Environment

Copy `.env.example` if you need to override the API base URL:

```bash
cp .env.example .env
```

Default:

```env
VITE_API_BASE_URL=/api
```

## Production

This app is built as static assets and served at `/admin/` behind the shared nginx reverse proxy from the workspace `docker-compose.yml`.
