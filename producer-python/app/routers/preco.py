from fastapi import APIRouter
import random
from datetime import datetime, timezone
from typing import List
from app.domain.generators import BASE_PRICES, generate_quote
from app.domain.models import Quote, QuotesEnvelope

router = APIRouter()


@router.get("/preco", tags=["Quotes"])
def get_preco():
    symbols = list(BASE_PRICES.keys())
    ativo = random.choice(symbols)
    quote = generate_quote(ativo)

    return {"ativo": quote["symbol"], "preco": quote["price"]}


@router.get("/precos", response_model=QuotesEnvelope, tags=["Quotes"])
def get_all_quotes():
    symbols = list(BASE_PRICES.keys())

    quotes: List[Quote] = [Quote(**generate_quote(sym)) for sym in symbols]

    generated_at = datetime.now(timezone.utc).isoformat()

    return {"generated_at": generated_at, "quotes": quotes}
