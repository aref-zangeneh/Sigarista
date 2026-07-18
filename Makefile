.PHONY: up up-all admin-reload

# Rebuild UI containers and apply local source changes.
up:
	docker compose up -d --build --force-recreate admin-ui public-ui proxy

# Rebuild and start the full stack.
up-all:
	docker compose up -d --build --force-recreate

# Rebuild admin UI from mounted source without recreating images.
admin-reload:
	docker compose restart admin-ui
