FROM node:22-alpine AS deps
WORKDIR /app
COPY frontend/wallets-hub-web/package*.json ./
RUN npm ci

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY frontend/wallets-hub-web/ .
ARG API_INTERNAL_URL=http://walletshub-api:8080
ARG NEXT_PUBLIC_BASE_PATH=/wallets
ENV API_INTERNAL_URL=$API_INTERNAL_URL
ENV NEXT_PUBLIC_BASE_PATH=$NEXT_PUBLIC_BASE_PATH
RUN npm run build

FROM node:22-alpine
WORKDIR /app
ENV NODE_ENV=production PORT=3000 HOSTNAME=0.0.0.0
RUN addgroup -S walletshub && adduser -S walletshub -G walletshub
COPY --from=build --chown=walletshub:walletshub /app/.next/standalone ./
COPY --from=build --chown=walletshub:walletshub /app/.next/static ./.next/static
COPY --from=build --chown=walletshub:walletshub /app/public ./public
USER walletshub
EXPOSE 3000
CMD ["node", "server.js"]
