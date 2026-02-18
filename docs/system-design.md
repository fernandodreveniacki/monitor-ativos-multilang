# System Design

## Estrutura Geral

O projeto está organizado em dois serviços independentes:

- `producer-python/`
- `processor-dotnet/`

Cada serviço possui estrutura própria, mantendo isolamento lógico.

---

## Producer — Estrutura Planejada
- `producer-python/`
    - `app/`
    - `routers/`
    - `domain/`
    - `services/`
    - `utils/`
    - `tests/`


### Responsabilidades Planejadas

- `routers/` → definição dos endpoints HTTP
- `domain/` → modelos e lógica de domínio
- `services/` → lógica de geração de cotações
- `utils/` → utilitários (ex.: logger)
- `tests/` → testes unitários futuros

---

## Processor — Estrutura Planejada

- `processor-dotnet/`
    - `src/`
    - `Application/`
    - `Domain/`
    - `Infrastructure/`
    - `Worker/`
    - `tests/`

---

### Camadas

- **Domain** → Entidades e regras de negócio
- **Application** → Casos de uso e serviços
- **Infrastructure** → Persistência e integrações externas
- **Worker** → Orquestração do processamento periódico

---

## Versionamento de Pastas Vazias (.gitkeep)

Algumas pastas podem conter um arquivo chamado `.gitkeep`.

O Git não versiona diretórios vazios por padrão.
Para preservar a estrutura inicial do projeto, utiliza-se `.gitkeep` nas pastas que ainda não possuem arquivos implementados.

Conforme os arquivos reais forem adicionados, o `.gitkeep` poderá ser removido.