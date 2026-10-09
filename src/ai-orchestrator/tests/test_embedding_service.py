import pytest
from ai_orchestrator.services.embedding_service import EmbeddingService


class FakeEmbeddingModel:
    def __init__(
        self,
        embeddings: list[list[float]],
    ) -> None:
        self._embeddings = embeddings
        self.last_texts: list[str] | None = None

    def encode(
        self,
        texts: list[str],
    ) -> list[list[float]]:
        self.last_texts = texts
        return self._embeddings


def test_similarity_uses_batch_embeddings() -> None:
    model = FakeEmbeddingModel(
        embeddings=[
            [1.0, 0.0],
            [0.8, 0.6],
        ]
    )

    service = EmbeddingService(model)

    score = service.similarity(
        "first text",
        "second text",
    )

    assert score == pytest.approx(0.8)
    assert model.last_texts == [
        "first text",
        "second text",
    ]


def test_similarity_rejects_wrong_embedding_count() -> None:
    model = FakeEmbeddingModel(
        embeddings=[
            [1.0, 0.0],
        ]
    )

    service = EmbeddingService(model)

    with pytest.raises(
        ValueError,
        match="Embedding count does not match input text count",
    ):
        service.similarity(
            "first",
            "second",
        )


def test_similarity_rejects_different_embedding_dimensions() -> None:
    model = FakeEmbeddingModel(
        embeddings=[
            [1.0, 0.0],
            [0.8, 0.6, 0.1],
        ]
    )

    service = EmbeddingService(model)

    with pytest.raises(
        ValueError,
        match="Embeddings must have the same dimension",
    ):
        service.similarity(
            "first",
            "second",
        )