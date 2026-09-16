FROM node:24.13.1-alpine AS build
WORKDIR /source
RUN npm install --global pnpm@12.4.2
COPY package.json pnpm-workspace.yaml pnpm-lock.yaml .npmrc ./
COPY apps/web/package.json apps/web/package.json
RUN pnpm install --frozen-lockfile
COPY apps/web/ apps/web/
RUN pnpm build

FROM nginx:1.28.2-alpine AS runtime
COPY infra/nginx.conf /etc/nginx/nginx.conf
COPY --from=build /source/apps/web/dist /usr/share/nginx/html
USER nginx
EXPOSE 8080
ENTRYPOINT ["nginx", "-g", "daemon off;"]
