from typing import Annotated

from pydantic import BaseModel, StringConstraints

ValidatedText = Annotated[
    str,
    StringConstraints(
        strip_whitespace=True,
        min_length=1,
        max_length=1000,
    ),
]


class HealthResponse(BaseModel):
    name: str
    version: str
    status: str


class TextValidationRequest(BaseModel):
    text: ValidatedText


class TextValidationResponse(BaseModel):
    text: ValidatedText
    length: int


class SemanticSimilarityRequest(BaseModel):
    left: ValidatedText
    right: ValidatedText


class SemanticSimilarityResponse(BaseModel):
    score: float