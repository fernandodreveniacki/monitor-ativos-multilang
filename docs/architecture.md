# Arquitetura do Sistema

## Versão

Documento referente à versão inicial (v1) do projeto.
A implementação será realizada incrementalmente conforme o roadmap.

---

## Visão Arquitetural

O Sistema foi projetado como uma arquitetura distribuída composta por dois serviços independentes:

- **Producer (Python + FastAPI)**
- **Processor (.NET Worker)**

A comunicação entre os serviços será realizada via HTTP dentro da rede do Docker Compose.

---

## Componentes

### Producer (Python)

Responsável por:

- Simular cotações de ativos financeiros
- Expor endpoints HTTP
- Fornecer dados estruturados para consumo

Não possuirá responsabilidade de persistência.

---

### Processor (.NET)

Responsável por:

- Consumir periodicamente o Producer
- Aplicar regras de processamento (se necessário)
- Persistir os dados no Supabase (Postgres)

---

### Supabase (Postgres)

Responsável por:

- Armazenamento persistente das cotações
- Manter histórico de eventos
- Permitir futuras consultas analíticas

---

## Comunicação entre Containers

Dentro do Docker Compose, os serviços se comunicarão via nome do serviço: 
`http://python-producer:8000`

Nunca será utilizado `localhost`, pois dentro de containers isso referencia o próprio container.

---

## Princípios Arquiteturais

- Separação de responsabilidades
- Baixo acoplamento entre serviços
- Configuração via variáveis de ambiente
- Evolução incremental via versionamento