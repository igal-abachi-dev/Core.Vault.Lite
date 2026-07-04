FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json tsconfig.base.json ./
COPY packages ./packages
COPY apps ./apps
RUN npm install --workspaces --include-workspace-root
RUN npm run build --workspace @finance-agent/agent-service

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
COPY --from=build /app/package.json ./package.json
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/packages ./packages
COPY --from=build /app/apps ./apps
CMD ["npm", "run", "start", "--workspace", "@finance-agent/agent-service"]
