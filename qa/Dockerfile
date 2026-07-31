FROM mcr.microsoft.com/playwright:v1.55.0-noble

WORKDIR /work/qa

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY validate.mjs lighthouse.mjs html-validate.mjs css-validate.mjs ./

CMD ["npm", "run", "validate"]
