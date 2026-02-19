# Roadmap

## Versão 1 — v1 (MVP Funcional)

### Estrutura e Arquitetura
- [x] Estrutura inicial de diretórios
- [x] Definição arquitetural (Producer + Processor)
- [x] Comunicação entre containers via Docker Compose

### Producer (Python + FastAPI)
- [x] Endpoint /health
- [x] Endpoint /preco
- [x] Endpoint /precos
- [x] Endpoint /quotes?symbols=
- [x] Dockerfile configurado

### Processor (.NET 8 Worker)
- [x] Implementação de BackgroundService
- [x] Polling periódico configurável
- [x] Integração com HttpClient
- [x] Retry com backoff exponencial
- [x] Logs estruturados com cycleId
- [x] Dockerfile configurado

### Persistência
- [x] Integração com Supabase (PostgreSQL)
- [x] Tabela asset_quotes
- [x] Regra PRICE_THRESHOLD
- [x] Idempotência via controle de inserção

## Próximas Evoluções
### Observabilidade
- [ ] Métricas Prometheus
- [ ] Dashboard Grafana
- [ ] Healthcheck do Processor
- [ ] Structured logging com sink externo

### Arquitetura
- [ ] Introduzir mensageria (RabbitMQ ou Kafka)
- [ ] Separar camada de domínio no Processor
- [ ] Adicionar testes automatizados

### Performance & Resiliência
- [ ] Cache temporário de cotações
- [ ] Circuit breaker
- [ ] Configuração dinâmica de ativos via banco

### Infraestrutura
- [ ] Docker multi-stage otimizado
- [ ] CI/CD básico (GitHub Actions)
- [ ] Deploy em ambiente cloud