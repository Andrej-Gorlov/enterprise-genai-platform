from collections.abc import Iterator
from uuid import UUID

import pytest
from ai_orchestrator.dependencies import get_embedding_service
from ai_orchestrator.main import app
from ai_orchestrator.service_info import get_service_info
from fastapi.testclient import TestClient


class FakeEmbeddingService:
    def similarity(
        self,
        left: str,
        right: str,
    ) -> float:
        return 0.75


def get_fake_embedding_service() -> FakeEmbeddingService:
    return FakeEmbeddingService()


@pytest.fixture
def client() -> Iterator[TestClient]:
    test_client = TestClient(app)

    try:
        yield test_client
    finally:
        app.dependency_overrides.clear()
        test_client.close()


def test_health_returns_healthy(
    client: TestClient,
) -> None:
    response = client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "healthy"
    assert data["name"] == "ai-orchestrator"
    assert data["version"] == get_service_info()["version"]


def test_validate_text_returns_length(
    client: TestClient,
) -> None:
    text = "Enterprise GenAI Platform"
    response = client.post(
        "/validate-text",
        json={"text": text},
    )
    assert response.status_code == 200
    data = response.json()
    assert data["text"] == text
    assert data["length"] == len(text)


def test_semantic_similarity_returns_service_score(
    client: TestClient,
) -> None:
    app.dependency_overrides[get_embedding_service] = get_fake_embedding_service

    response = client.post(
        "/semantic-similarity",
        json={
            "left": "Enterprise GenAI Platform",
            "right": "Enterprise AI Platform",
        },
    )

    assert response.status_code == 200
    assert response.json()["score"] == 0.75


def test_semantic_similarity_rejects_blank_text(
    client: TestClient,
) -> None:
    app.dependency_overrides[get_embedding_service] = get_fake_embedding_service

    left = "   "
    right = "valid text"
    response = client.post(
        "/semantic-similarity",
        json={"left": left, "right": right},
    )
    assert response.status_code == 422
    body = response.json()
    assert body["detail"][0]["type"] == "string_too_short"
    assert body["detail"][0]["loc"] == ["body", "left"]


def test_correlation_id_preserves_supplied_value(
    client: TestClient,
) -> None:
    response = client.get(
        "/health",
        headers={
            "X-Correlation-ID": "stage-26-5-python",
        },
    )

    assert response.status_code == 200
    assert response.headers["X-Correlation-ID"] == "stage-26-5-python"


def test_correlation_id_generates_when_missing(
    client: TestClient,
) -> None:
    response = client.get("/health")

    assert response.status_code == 200
    correlation_id = response.headers.get("X-Correlation-ID")
    assert correlation_id is not None
    assert str(UUID(correlation_id)) == correlation_id