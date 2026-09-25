# ADR-001 — Use .NET 10 and Python for backend workloads

Status: Accepted

Date: 2026-09-24

## Context

Enterprise GenAI Platform содержит как традиционные
enterprise backend-компоненты, так и AI/ML-компоненты.

Core backend должен реализовывать:

- authentication / authorization;
- business logic;
- corporate integrations;
- platform API;
- audit;
- orchestration прикладных операций.

AI-компоненты должны реализовывать:

- RAG;
- embeddings;
- reranking;
- agent workflows;
- AI evaluation;
- интеграцию с AI/ML ecosystem.

Необходимо выбрать технологический стек,
позволяющий эффективно реализовать обе группы задач.

## Decision

Использовать .NET 10 / ASP.NET Core
для Core API и инфраструктурных backend-компонентов.

Использовать Python / FastAPI
для AI-specific workloads.

Языки не должны использоваться произвольно.

.NET является основным стеком enterprise/business слоя.

Python применяется там,
где AI/ML ecosystem даёт техническое преимущество.

## Alternatives Considered

### Alternative A — Только .NET

Плюсы:

- единый язык и runtime;
- проще CI/CD;
- проще сопровождение.

Минусы:

- менее удобный доступ к части AI/ML ecosystem;
- сложнее использовать некоторые Python-first библиотеки,
  модели и исследовательские инструменты.

### Alternative B — Только Python

Плюсы:

- единый backend stack;
- удобная AI/ML ecosystem.

Минусы:

- отказ от сильной .NET enterprise-базы;
- business/security/integration слой пришлось бы переносить
  в менее привычный для команды стек.

### Alternative C — .NET + Python

Использовать каждый стек в той области,
где он имеет наибольшие преимущества.

## Consequences

### Positive

- используем сильные стороны обеих экосистем;
- AI experimentation проще;
- business/security слой остаётся в .NET;
- Python AI services можно масштабировать независимо.

### Negative

- два языка;
- два dependency ecosystem;
- два runtime;
- сложнее CI/CD;
- сложнее tracing/debugging между сервисами;
- необходимо поддерживать API contracts между .NET и Python.

## Related Requirements (Связанные требования)

- Security;
- Scalability (Масштабируемость);
- Maintainability (Удобообслуживаемость);
- возможность независимого масштабирования AI workloads.

## Related ADR (Связанный ADR)

- ADR-003 — Application Architecture (Архитектура приложения);
- ADR-004 — Sync and Async Communication (Синхронная и асинхронная коммуникация).
