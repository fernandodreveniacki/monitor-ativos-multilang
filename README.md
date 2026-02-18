# Monitor de Ativos Financeiros — Multi-language
## Versão Atual

Esta é a **versão inicial (v1)** do projeto, contendo:
- Estrutura arquitetural definida
- Organização dos serviços
- Documentação técnica inicial
- Roadmap de implementação

A implementação dos serviços será feita incrementalmente conforme os commits seguintes.

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