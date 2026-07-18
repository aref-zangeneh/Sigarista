FROM node:24-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm install --no-audit --no-fund

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]

FROM node:24-alpine AS dev
RUN apk add --no-cache nginx inotify-tools && mkdir -p /usr/share/nginx/html /run/nginx /etc/nginx/http.d
COPY nginx.conf /etc/nginx/http.d/default.conf
COPY docker-entrypoint.dev.sh /docker-entrypoint.dev.sh
RUN chmod +x /docker-entrypoint.dev.sh && sed -i 's/\r$//' /docker-entrypoint.dev.sh

WORKDIR /app
EXPOSE 80
ENTRYPOINT []
CMD ["/bin/sh", "/docker-entrypoint.dev.sh"]
