from fastapi import APIRouter, Query
from typing import List
from datetime import datetime, timezone

from app.domain.models import QuotesEnvelope, Quote
from app.domain.generators import generate_quote

router = APIRouter()


@router.get("/quotes", response_model=QuotesEnvelope, tags=["Quotes"])
def get_quotes(
    symbols: str = Query(
        ...,
        description="Lista de símbolos separados por vírgula.  Ex: BTCUSD,AAPL,PETR4",
    ),
):
    symbol_list: List[str] = [s.strip() for s in symbols.split(",") if s.strip()]
    quotes = [Quote(**generate_quote(sym)) for sym in symbol_list]

    generated_at = datetime.now(timezone.utc).isoformat()
    return {"generated_at": generated_at, "quotes": quotes}
