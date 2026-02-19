# Monitor de Ativos Financeiros — Multi-language

Sistema distribuído para monitoramento de ativos financeiros, implementado com arquitetura multi-linguagem e orquestrado via Docker Compose.

## Versão Atual
Esta é a **versão funcional inicial (v1)** do projeto, contendo:
- Arquitetura multi-linguagem definida
- Producer implementado (FastAPI)
- Processor implementado (.NET Worker)
- Persistência no Supabase (PostgreSQL Cloud)
- Retry com backoff exponencial
- Idempotência via UNIQUE + ON CONFLICT
- Logs estruturados com correlation id (cycleId)
- Métricas simples em memória
- Orquestração via Docker Compose

A evolução futura está descrita em docs/roadmap.md.

---
## Status da Versão

- [x] Estrutura inicial do projeto
- [x] Documentação arquitetural
- [x] Implementação do Producer
- [x] Implementação do Processor
- [x] Persistência no Supabase
- [x] Dockerfiles otimizados
- [x] Guia de execução
- [ ] Testes automatizados no Producer

---

## Visão Geral

Este projeto implementa um sistema de monitoramento de ativos financeiros composto por dois serviços:

- **Producer (Python + FastAPI)** — responsável por simular cotações.
- **Processor (.NET Worker)** — responsável por consumir periodicamente as cotações e persistir no Supabase (Postgres).

A orquestração será feita via Docker Compose.

---

## Arquitetura Resumida

Fluxo planejado:

Producer → Processor → Supabase

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

## Documentação Técnica

Documentação detalhada disponível em:
- `docs/architecture.md`
- `docs/system-design.md`
- `docs/flow.md`
- `docs/roadmap.md`

---

## Status
- [x] Estrutura inicial do projeto
- [x] Documentação arquitetural
- [ ] Implementação do Producer
- [ ] Implementação do Processor
- [ ] Persistência no Supabase
- [ ] Dockerfiles otimizados
- [ ] Guia de execução

---

## Estrutura da pasta docs/

### docs/architecture.md
- responsabilidades dos serviços
- comunicação entre containers
- justificativas arquiteturais

### docs/system-design.md
- estrutura interna de pastas
- camadas (Domain, Application, Infrastructure)
- explicação do `.gitkeep`

### docs/flow.md
- fluxo de execução
- polling
- persistência

### docs/roadmap.md
- checklist evolutivo
- milestones