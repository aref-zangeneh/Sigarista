.PHONY: up up-all admin-reload pos-print-server test-pos-print deploy backup restore release server-deploy

# Rebuild UI containers and apply local source changes.
up:
	docker compose up -d --build --force-recreate admin-ui public-ui proxy

# Rebuild and start the full stack.
up-all:
	docker compose up -d --build --force-recreate

# Rebuild admin UI from mounted source without recreating images.
admin-reload:
	docker compose restart admin-ui

# Production deploy: pull, back up the database, rebuild, restart, verify.
deploy:
	./scripts/deploy.sh

# Dump the database into backups/.
backup:
	./scripts/backup-db.sh

# Replace the database from a backup file: make restore FILE=backups/xxx.sql.gz
restore:
	./scripts/restore-db.sh $(FILE)

# Build production images on this machine and push them to the registry.
release:
	./scripts/release.sh

# On the server: pull the pushed images, back up the DB, restart, verify.
server-deploy:
	./scripts/server-deploy.sh

# Local automatic receipt printer agent (customer + barista).
pos-print-server:
	@if [ ! -d scripts/pos-print/node_modules/sharp ]; then npm install --prefix scripts/pos-print; fi
	@if [ -f scripts/pos-print.env ]; then set -a && . scripts/pos-print.env && set +a; fi; \
	node scripts/pos-print-server.mjs

# Send sample receipts through the local print agent.
test-pos-print:
	bash scripts/test-pos-print.sh
