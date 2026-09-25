# ADR-003 — Application Architecture

Status: Accepted

Date: 2026-09-24

## Context

Enterprise GenAI Platform содержит как тесно связанную
business-функциональность, так и специализированные workloads
с существенно отличающимися требованиями.

Core business backend включает:

- Users;
- Chats;
- Documents;
- Authorization;
- Audit;
- Integrations.

Эти функции используют один технологический стек,
имеют тесно связанную бизнес-модель и на начальном этапе
не требуют независимого масштабирования и deployment.

Одновременно существуют специализированные workloads (рабочие нагрузки):

- AI Orchestrator;
- Document Ingestion Worker;
- LLM Gateway.

Они отличаются по runtime, характеру нагрузки,
масштабированию и failure modes.

Необходимо избежать как чрезмерно связанного монолита,
так и преждевременного дробления системы на множество микросервисов.

## Decision (Решение)

Core API реализуется как Modular Monolith на .NET 10.

Внутри Core API выделяются логические модули
с явно определёнными границами ответственности:

- Users;
- Chats;
- Documents;
- Authorization;
- Audit;
- Integrations.

Модули работают внутри одного deployable application,
но не должны произвольно обращаться к внутренним данным
друг друга в обход определённых контрактов.

Специализированные workloads выделяются
в отдельные deployable services (развертываемые службы):

- AI Orchestrator — Python / FastAPI;
- Document Ingestion Worker — Python;
- LLM Gateway — .NET 10.

Новый микросервис создаётся только при наличии
технически обоснованной причины:

- независимое масштабирование;
- отдельный runtime;
- отдельный lifecycle;
- существенно иной resource profile;
- отдельная security boundary;
- отдельные failure characteristics.

## Alternatives Considered (Рассмотренные альтернативы)

### Alternative A — Full Monolith

Вся функциональность платформы размещается
в одном приложении и одном процессе.

Плюсы:

- простая разработка и deployment (развертывание);
- минимальное количество сетевых взаимодействий;
- простая локальная разработка;
- меньше operational complexity (операционная сложность).

Минусы:

- AI workloads нельзя независимо масштабировать;
- Python AI ecosystem сложнее интегрировать;
- сбой тяжёлой AI-задачи может повлиять на основной API;
- разные типы нагрузки конкурируют за одни ресурсы.

### Alternative B — Full Microservices

Каждая функциональная область реализуется
отдельным deployable service (развертываемая служба):

User Service;
Chat Service;
Document Service;
Authorization Service;
Audit Service и т. д.

Плюсы:

- независимое масштабирование;
- независимый deployment;
- чёткие runtime boundaries (границы времени выполнения).

Минусы:

- большое количество сервисов;
- сложнее observability (наблюдаемость);
- больше сетевых взаимодействий;
- сложнее distributed transactions (распределенные транзакции);
- сложнее debugging;
- выше требования к DevOps;
- значительная operational complexity (операционная сложность) без доказанной необходимости.

### Alternative C — Modular Monolith + Specialized Services

Core business-функциональность остаётся
в одном modular monolith.

Только workloads с объективными отличиями
выносятся в отдельные services.

## Consequences (Последствия)

### Positive

- сохраняются чёткие архитектурные границы;
- ниже operational complexity, чем у полной microservice architecture;
- AI workloads могут независимо масштабироваться;
- Python и .NET используются в подходящих областях;
- Core API проще разрабатывать и отлаживать;
- остаётся возможность позднее вынести отдельный модуль
  в самостоятельный сервис при появлении необходимости.

### Negative

- система всё равно является распределённой;
- появляются сетевые вызовы между .NET и Python;
- необходимо поддерживать API contracts;
- требуется distributed tracing;
- отказ специализированного сервиса может временно
  сделать связанную функцию недоступной;
- разработчики должны соблюдать границы модулей
  внутри modular monolith.

## Related Requirements (Связанные требования)

- Availability (Доступность);
- Scalability (Масштабируемость);
- Maintainability (Удобообслуживаемость);
- независимое масштабирование AI workloads;
- разделение ответственности компонентов.

## Related ADR (Связанные ADR)

- ADR-001 — Use .NET 10 and Python;
- ADR-002 — PostgreSQL and pgvector;
- ADR-004 — Sync and Async Communication;
- ADR-005 — Background Job Mechanism;
- ADR-006 — LLM Routing and Data Classification.
