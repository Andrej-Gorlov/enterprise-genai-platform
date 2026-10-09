from typing import Annotated, cast

from fastapi import Depends, Request

from ai_orchestrator.contracts import (
    EmbeddingServiceProtocol,
    TextServiceProtocol,
)
from ai_orchestrator.services.text_service import TextService


def get_text_service() -> TextService:
    return TextService()


TextServiceDependency = Annotated[
    TextServiceProtocol,
    Depends(get_text_service),
]


def get_embedding_service(
    request: Request,
) -> EmbeddingServiceProtocol:
    return cast(
        EmbeddingServiceProtocol,
        request.app.state.embedding_service,
    )


EmbeddingServiceDependency = Annotated[
    EmbeddingServiceProtocol,
    Depends(get_embedding_service),
]