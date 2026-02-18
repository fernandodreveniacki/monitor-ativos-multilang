from datetime import datetime, timezone
import random

BASE_PRICES = {
    "BTCUSD": 52000.0,
    "ETHUSD": 2800.0,
    "AAPL": 190.0,
    "MSFT": 410.0,
    "PETR4": 38.0,
    "VALE3": 70.0,
}


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def generate_quote(symbol: str) -> dict:
    base = BASE_PRICES.get(symbol.upper(), 100.0)

    # Variação percentual simulada (-1.5% a +1.5%)
    change_pct = round(random.uniform(-1.5, 1.5), 4)

    # Preço final baseado na variação
    price = round(base * (1 + change_pct / 100), 4)

    return {
        "symbol": symbol.upper(),
        "price": price,
        "change_pct": change_pct,
        "quoted_at": utc_now_iso(),
    }
