from pydantic import BaseModel
from typing import List


class Quote(BaseModel):
    symbol: str
    price: float
    change_pct: float
    quoted_at: str


class QuotesEnvelope(BaseModel):
    generated_at: str
    quotes: List[Quote]
