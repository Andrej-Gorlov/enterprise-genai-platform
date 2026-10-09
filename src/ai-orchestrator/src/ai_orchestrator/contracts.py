from typing import Protocol

from ai_orchestrator.models import TextValidationResponse


class TextServiceProtocol(Protocol):
    def validate(self, text: str) -> TextValidationResponse:
        ...


class EmbeddingModelProtocol(Protocol):
    def encode(
        self,
        texts: list[str],
    ) -> list[list[float]]:
        ...


class EmbeddingServiceProtocol(Protocol):
    def similarity(
        self,
        left: str,
        right: str,
    ) -> float:
        ...