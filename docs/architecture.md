# Arquitetura do Sistema

## Versão

Documento referente à versão funcional inicial (v1).

---

## Visão Arquitetural

O sistema foi projetado como uma arquitetura distribuída composta por dois serviços:

- **Producer (Python + FastAPI)**
- **Processor (.NET 8 Worker Service)**

A comunicação entre os serviços ocorre via HTTP dentro da rede do Docker Compose.

---

## Componentes

### Producer (Python)

Responsável por:

- Simular cotações de ativos financeiros
- Expor endpoints HTTP
- Fornecer dados estruturados para consumo

Não possui responsabilidade de persistência.

### Endpoints disponíveis
- `GET /preco`
Retorna uma única cotação simulada.
- `GET /precos`
Retorna a lista completa de ativos simulados.
- `GET /quotes?symbols=BTCUSD,AAPL,PETR4`
Permite filtrar ativos específicos via query string.

Exemplo de resposta:
```
{
  "generated_at": "2026-02-19T15:00:00Z",
  "quotes": [
    {
      "symbol": "BTCUSD",
      "price": 182.45,
      "change_pct": 1.24,
      "quoted_at": "2026-02-19T15:00:00Z"
    },
    {
      "symbol": "AAPL",
      "price": 150.12,
      "change_pct": -0.42,
      "quoted_at": "2026-02-19T15:00:00Z"
    }
  ]
}
```
---

### Processor (.NET)

Responsável por:

- Executar polling periódico
- Consumir o Producer
- Aplicar a regra `PRICE_THRESHOLD`
- Persistir dados no Supabase (PostgreSQL)
- Gerar logs estruturados com `cycleId`

---

### Supabase (PostgreSQL)

Responsável por:

- Armazenamento persistente das cotações
- Manutenção do histórico de preços
- Suporte a futuras consultas analíticas

---

## Comunicação entre Containers

Dentro do Docker Compose, os serviços se comunicam via nome do serviço:

http://python-producer:8000

`localhost` não é utilizado, pois dentro de containers ele referencia o próprio container.
