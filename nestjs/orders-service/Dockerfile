FROM node:20-slim
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends openssl && rm -rf /var/lib/apt/lists/*

COPY package*.json ./
COPY prisma ./prisma
RUN npm install

COPY tsconfig*.json ./
RUN npx prisma generate

COPY src ./src
RUN npm run build

EXPOSE 5001
CMD ["npm", "run", "start:prod"]
