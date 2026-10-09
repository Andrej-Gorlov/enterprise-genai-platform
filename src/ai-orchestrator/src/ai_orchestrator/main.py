import logging
import re
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from uuid import uuid4

from fastapi import FastAPI, Request, Response
from starlette.middleware.base import RequestResponseEndpoint

from ai_orchestrator.dependencies import (
    EmbeddingServiceDependency,
    TextServiceDependency,
)
from ai_orchestrator.models import (
    HealthResponse,
    SemanticSimilarityRequest,
    SemanticSimilarityResponse,
    TextValidationRequest,
    TextValidationResponse,
)
from ai_orchestrator.service_info import get_service_info
from ai_orchestrator.services.embedding_service import EmbeddingService
from ai_orchestrator.services.sentence_transformer_model import SentenceTransformerModel

logger = logging.getLogger("uvicorn.error")

CORRELATION_ID_HEADER = "X-Correlation-ID"
CORRELATION_ID_PATTERN = re.compile(
    r"[A-Za-z0-9_.-]{1,128}\Z"
)

@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    model = SentenceTransformerModel(
        "sentence-transformers/all-MiniLM-L6-v2"
    )

    app.state.embedding_service = EmbeddingService(model)

    yield


app = FastAPI(
    title="Enterprise GenAI Platform AI Orchestrator",
    lifespan=lifespan,
)


@app.middleware("http")
async def correlation_id_middleware(
    request: Request,
    call_next: RequestResponseEndpoint,
) -> Response:
    supplied_id = request.headers.get(CORRELATION_ID_HEADER)

    if supplied_id and CORRELATION_ID_PATTERN.fullmatch(supplied_id):
        correlation_id = supplied_id
    else:
        correlation_id = str(uuid4())

    request.state.correlation_id = correlation_id

    try:
        response = await call_next(request)
    except Exception:
        logger.exception(
            "HTTP %s %s failed. CorrelationId: %s",
            request.method,
            request.url.path,
            correlation_id,
        )
        raise

    response.headers[CORRELATION_ID_HEADER] = correlation_id

    logger.info(
        "HTTP %s %s responded %s. CorrelationId: %s",
        request.method,
        request.url.path,
        response.status_code,
        correlation_id,
    )

    return response


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    service_info = get_service_info()

    return HealthResponse(
        name=service_info["name"],
        version=service_info["version"],
        status="healthy",
    )


@app.post("/validate-text", response_model=TextValidationResponse)
def validate_text(
    request: TextValidationRequest,
    text_service: TextServiceDependency,
) -> TextValidationResponse:
    return text_service.validate(request.text)


@app.post("/semantic-similarity", response_model=SemanticSimilarityResponse)
def semantic_similarity(
    request: SemanticSimilarityRequest,
    embedding_service: EmbeddingServiceDependency,
) -> SemanticSimilarityResponse:
    score = embedding_service.similarity(
        left=request.left,
        right=request.right,
    )

    return SemanticSimilarityResponse(score=score)