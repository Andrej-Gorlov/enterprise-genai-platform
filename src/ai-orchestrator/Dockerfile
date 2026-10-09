FROM python:3.13-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    HF_HOME=/home/app/.cache/huggingface

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgomp1 \
    && rm -rf /var/lib/apt/lists/*

# CPU-only PyTorch — без CUDA runtime
RUN python -m pip install --no-cache-dir \
    "torch==2.11.0" \
    --index-url https://download.pytorch.org/whl/cpu

COPY pyproject.toml ./
COPY src ./src

RUN python -m pip install --no-cache-dir .

RUN useradd --system --create-home app \
    && mkdir -p "$HF_HOME" \
    && chown -R app:app /home/app/.cache

USER app

EXPOSE 8000

CMD ["python", "-m", "uvicorn", "ai_orchestrator.main:app", "--host", "0.0.0.0", "--port", "8000"]