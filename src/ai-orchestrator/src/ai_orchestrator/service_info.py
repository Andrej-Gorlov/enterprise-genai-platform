from importlib.metadata import version


def get_service_info() -> dict[str, str]:
    return {
        "name": "ai-orchestrator",
        "version": version("ai-orchestrator"),
    }