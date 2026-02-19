create table if not exists public.asset_quotes (
  id bigserial primary key,
  symbol text not null,
  price numeric not null,
  change_pct numeric not null,
  quoted_at timestamptz not null,
  created_at timestamptz not null default now()
);

create index if not exists idx_asset_quotes_symbol_quoted_at
on public.asset_quotes(symbol, quoted_at desc);

create unique index if not exists uq_asset_quotes_symbol_quoted_at
on public.asset_quotes(symbol, quoted_at);
