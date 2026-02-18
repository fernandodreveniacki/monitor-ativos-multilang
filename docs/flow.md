# Fluxo de Execução

## Execução Planejada

1. Docker Compose iniciará o Producer.
2. O Processor será iniciado após o healthcheck do Producer.
3. O Processor executará polling periódico.
4. O endpoint `/quotes` será consumido.
5. As cotações serão persistidas no Supabase.

---

## Polling

O Processor utilizará um intervalo configurável via variável de ambiente:

- `POLL_INTERVAL_SECONDS`

---

## Variáveis de Ambiente Planejadas

- `PRODUCER_BASE_URL`
- `SUPABASE_DB_CONNECTION`
- `SYMBOLS`
- `POLL_INTERVAL_SECONDS`

---

## Persistência

Os dados serão armazenados em uma tabela planejada:

`public.asset_quotes`

Campos previstos:

- id
- symbol
- price
- change_pct
- quoted_at
- created_at
