FROM node:24.13.1-alpine AS build
WORKDIR /source
RUN npm install --global pnpm@12.4.2
COPY package.json pnpm-workspace.yaml pnpm-lock.yaml .npmrc ./
COPY apps/web/package.json apps/web/package.json
RUN pnpm install --frozen-lockfile
COPY apps/web/ apps/web/
RUN pnpm build

# The Alpine nginx package receives security fixes sooner than the official nginx image (1.28.3-r7 vs r1).
FROM alpine:3.23.3 AS runtime
RUN apk upgrade --no-cache && apk add --no-cache nginx \
    && mkdir -p /usr/share/nginx/html && chown -R nginx:nginx /usr/share/nginx/html
COPY infra/nginx.conf /etc/nginx/nginx.conf
COPY --from=build /source/apps/web/dist /usr/share/nginx/html
USER nginx
EXPOSE 8080
# -e stderr keeps nginx from opening its compiled-in log path on the read-only filesystem.
ENTRYPOINT ["nginx", "-e", "stderr", "-g", "daemon off;"]
