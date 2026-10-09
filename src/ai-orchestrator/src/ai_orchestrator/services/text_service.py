from ai_orchestrator.models import TextValidationResponse


class TextService:
    def validate(self, text: str) -> TextValidationResponse:
        return TextValidationResponse(
            text=text,
            length=len(text),
        )