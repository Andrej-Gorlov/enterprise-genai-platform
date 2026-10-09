import math

from ai_orchestrator.contracts import EmbeddingModelProtocol


class EmbeddingService:
    def __init__(
        self,
        model: EmbeddingModelProtocol,
    ) -> None:
        self._model = model

    def similarity(
        self,
        left: str,
        right: str,
    ) -> float:
        embeddings = self._model.encode([left, right])

        if len(embeddings) != 2:
            raise ValueError(
                "Embedding count does not match input text count."
            )

        return self._cosine_similarity(
            embeddings[0],
            embeddings[1],
        )

    @staticmethod
    def _cosine_similarity(
        left: list[float],
        right: list[float],
    ) -> float:
        if len(left) != len(right):
            raise ValueError(
                "Embeddings must have the same dimension."
            )

        dot_product = sum(
            left_value * right_value
            for left_value, right_value in zip(left, right)
        )

        left_magnitude = math.sqrt(
            sum(value**2 for value in left)
        )
        right_magnitude = math.sqrt(
            sum(value**2 for value in right)
        )

        if left_magnitude == 0 or right_magnitude == 0:
            return 0.0

        return dot_product / (
            left_magnitude * right_magnitude
        )