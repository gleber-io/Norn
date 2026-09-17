# norn-dashboard

Dashboard React da Fase 11 do Norn — grafo de topologia, timeline MAPE-K ao vivo, detalhe de
plano, série de métrica e controle de modo, tudo alimentado por REST + SignalR da `Norn.API`
(ver `docs/norn-api-contract.md` na raiz do repositório).

## Rodando em dev

```
npm install
npm run dev
```

Por padrão o dashboard chama a Norn.API na mesma origem (`VITE_API_BASE_URL` vazio). Em dev, a
Norn.API roda separada (`dotnet run --project src/Platform/Norn.API`) — aponte
`VITE_API_BASE_URL` num `.env.local` pra porta que ela imprimir no console, ex.:

```
VITE_API_BASE_URL=http://127.0.0.1:5080
```

## Gerando os tipos da API

```
npm run generate:api
```

Lê `${VITE_API_BASE_URL}/openapi/v1.json` (por padrão `http://localhost:5000`, ajuste o script em
`package.json` ou passe a URL certa) e regrava `src/lib/api-types.ts`. **Precisa da Norn.API
rodando localmente** — não roda no CI (o job de front-end não sobe a API; o arquivo gerado fica
commitado no git como qualquer outro código-fonte).

## Scripts

- `npm run dev` — servidor de desenvolvimento do Vite
- `npm run build` — typecheck (`tsc -b`) + build de produção
- `npm run test` — Vitest (jsdom + MSW, nunca bate na Norn.API real)
- `npm run lint` — `biome ci .` (lint + formatação num comando só)
- `npm run format` — aplica as correções do Biome

## Empacotamento pra produção — residual, não implementado nesta fase

O DoD da Fase 11 pede acessar o dashboard pela URL da própria Norn.API
(`UseStaticFiles`/`MapFallbackToFile`, sem CORS em runtime). Hoje isso é validado copiando
`dist/` manualmente pra `src/Platform/Norn.API/wwwroot/` depois de `npm run build` — o Dockerfile
da Norn.API, o manifesto K8s e o wiring no `bootstrap.ps1` (`npm run build` → `generate:api` →
`docker build`) ficam como residual explícito, mesma decisão já tomada pro `Norn.Worker` na
Fase 9 (ver `CLAUDE.md`, "Estado atual").
