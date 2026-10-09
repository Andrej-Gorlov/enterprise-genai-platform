from typing import cast

from sentence_transformers import SentenceTransformer


class SentenceTransformerModel:
    def __init__(
        self,
        model_name: str,
    ) -> None:
        self._model = SentenceTransformer(model_name)

    def encode(
        self,
        texts: list[str],
    ) -> list[list[float]]:
        embeddings = self._model.encode(
            texts,
            convert_to_numpy=True,
        )

        return cast(
            list[list[float]],
            embeddings.tolist(),
        )