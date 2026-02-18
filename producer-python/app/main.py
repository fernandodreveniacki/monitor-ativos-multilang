from fastapi import FastAPI
from app.routers import health

app = FastAPI(title="Financial Assets Producer", version="1.0.0")

app.include_router(health.router)
