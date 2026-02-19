# Monitor de Ativos Financeiros — Multi-language

Sistema distribuído para monitoramento de ativos financeiros, implementado com arquitetura multi-linguagem e orquestrado via Docker Compose.

## Versão Atual
Esta é a **versão funcional inicial (v1)** do projeto, contendo:
- Arquitetura distribuída (Producer + Processor)
- Producer implementado (Python + FastAPI)
- Processor implementado (.NET 8 Worker)
- Persistência condicional no Supabase (PostgreSQL Cloud)
- Regra de negócio: persistir apenas se `preco > PRICE_THRESHOLD`
- Retry com backoff exponencial
- Logs estruturados com `cycleId`
- Orquestração via Docker Compose
- Comunicação entre serviços via nome do container (`python-producer`)

A evolução futura está descrita em `docs/roadmap.md`.

---

## Visão Geral

Este projeto implementa um sistema de monitoramento de ativos financeiros composto por dois serviços:

- **Producer (FastAPI)**  
  Simula preços de ativos financeiros e expõe:
  - `GET /health`
  - `GET /preco`
  - `GET /precos`
  - `GET /quotes?symbols=BTCUSD,AAPL,PETR4`

- **Processor (.NET Worker)**  
  - Executa polling periódico (configurável)
  - Consome o Producer
  - Aplica regra `PRICE_THRESHOLD`
  - Persiste no Supabase se a condição for atendida
  - Gerar logs com `cycleId`

---

## Arquitetura Resumida

Fluxo:
```
Python Producer → .NET Processor → Supabase
```
O Processor consumirá o Producer via nome do serviço no Docker (`python-producer`), nunca via `localhost`.

---

## Estrutura do Repositório
```
monitor-ativos-multilang/
├── docs/
├── processor-dotnet/
├── producer-python/
├── sql/
├── .env.example
├── .gitignore
├── docker-compose.yml
└── README.md
```
---

## Como Rodar
### Pré-requisitos
Antes de iniciar, é necessário ter instalado:
- Docker
- Docker Compose
- Conta no Supabase

As dependências são instaladas automaticamente durante o build dos containers.

---
## Rodando com Docker (Recomendado)
Essa é a forma mais simples de executar o projeto.
### 1. Criar arquivo `.env`
exemplo:
```
cp .env.example .env
```
Edite o arquivo `.env` e preencha a connection string do Supabase:
```
SUPABASE_DB_CONNECTION=Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<PROJECT_REF>;Password=<PASSWORD>;SSL Mode=Require;

PRICE_THRESHOLD=150
POLL_INTERVAL_SECONDS=10
MAX_RETRIES=5
RETRY_BASE_DELAY_MS=500

```
---
### 2. Criar tabela no Supabase
Acesse o SQL Editor do Supabase e execute:
```
create table public.asset_quotes (
    id bigserial primary key,
    symbol text not null,
    price numeric not null,
    change_pct numeric not null default 0,
    quoted_at timestamptz not null default now()
);

```
---
### 3. Subir os containers
Na raiz do projeto, execute:
```
 docker compose up --build
```

O Docker irá:
- Construir o Producer (FastAPI)
- Construir o Processor (.NET Worker)
- Iniciar ambos os serviços
- Aguardar o healthcheck do Producer
- Iniciar o Processor automaticamente
---
### 4. Testar o Producer
**Endpoint `/preco`**
Retorna apenas 1 ativo por vez 
```
curl http://localhost:8000/preco
```

**Endpoint `/precos`**
Retorna a lista completa de ativos gerados
```
curl http://localhost:8000/precos
```
**Endpoint `/quotes`com filtro**
```
curl "http://localhost:8000/quotes?symbols=BTCUSD,AAPL"
```

---
### 5. Verificar logs do Processor
Para acompanhar a execução:
```
docker compose logs -f dotnet-processor
```
**Logs formatados**
Para visualizar apenas a mensagem formatada com `cycleId` e timestamp:
```
docker compose logs -f dotnet-processor \
| sed -n 's/^[^|]*| //p' \
| jq -r '"\(.Timestamp) [\(.Scopes[]? | select(.cycleId!=null).cycleId)] \(.Message)"'
```

Você verá logs como:
```
processor.cycle.start
processor.http.success ativo=BTCUSD preco=182.45
processor.db.commit ativo=BTCUSD preco=182.45
processor.cycle.end elapsedMs=820 fetched=1 inserted=1 retries=0 errors=0
```
---
### 6. Para os serviços
```
docker compose down
```
---
### Resultado esperado
A cada `POLL_INTERVAL_SECONDS` o Processor:
1. Consome o Producer
2. Verifica se preco > `PRICE_THRESHOLD`
3. Persiste no Supabase quando aplicável
4. Registra logs com `cycleId`

### Requisitos para logs formatados
É necessário ter instalado:
- jq
- sed
---
## Documentação 
- `docs/architecture.md`
- `docs/roadmap.md`
---
