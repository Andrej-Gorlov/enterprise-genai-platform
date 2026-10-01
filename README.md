# 🚀 Enterprise GenAI Platform

Enterprise GenAI Platform архитектурный проект корпоративной GenAI-платформы, который показывает поэтапную реализацию системы от архитектурного baseline до работающих продуктовых вертикалей.

Фокус проекта на построении управляемой enterprise платформы: с понятными границами ответственности, контролем доступа, наблюдаемостью, воспроизводимой инфраструктурой и возможностью безопасно наращивать RAG/LLM функциональность поверх стабильного Core API.

## 🧭 Архитектурная идея

Платформа строится от базовых enterprise компонентов к AI возможностям:

```text
Web UI
  |
  v
Core API
  |
  v
Application Modules
  |
  v
PostgreSQL / pgvector
  |
  v
AI Orchestrator / LLM Gateway / Ingestion
```

Ключевой принцип `не усложнять систему раньше времени.`

На текущем этапе Core API развивается как **Modular Monolith**, а AI специфичные workloads выделяются отдельно только там, где это действительно оправдано.

## ✅ Текущий статус

### Stage 0 Architecture

Завершён.

Зафиксированы:

- C4 Context и Container diagrams;
- NFR;
- Data Flows;
- Security Boundaries;
- ADR;
- Architecture Baseline;
- MVP Scope;
- границы между .NET Core API и Python AI workloads;
- PostgreSQL + pgvector;
- RabbitMQ;
- Object Storage;
- LLM Gateway;
- OIDC / OAuth2;
- audit и authorization boundaries.

### Stage 1 Full Stack MVP

Завершён базовый Full Stack контур:

```text
React + TypeScript
        |
        v
TanStack Query
        |
        v
ASP.NET Core .NET 10
        |
        v
EF Core / Npgsql
        |
        v
PostgreSQL + pgvector
```

Результат Stage 1 полноценная рабочая вертикаль:

```text
React -> .NET -> PostgreSQL
```

Она создаёт техническую основу для дальнейшей реализации:

- пользователей;
- проектов;
- документов;
- чатов;
- истории запросов.

Первым реализованным вертикальным срезом стал модуль `Chats`.

## 🏗️ Что уже реализовано

### Core API

- ASP.NET Core на .NET 10;
- Dependency Injection;
- middleware pipeline;
- configuration и Options pattern;
- EF Core;
- Npgsql;
- migrations;
- PostgreSQL;
- pgvector;
- REST API conventions;
- validation;
- ProblemDetails;
- error handling;
- CORS;
- Correlation ID;
- structured request logging;
- database-aware health checks.

### Frontend

- React;
- TypeScript;
- Vite;
- API client boundary;
- TanStack Query;
- query-based loading;
- mutation-based create flow;
- cache invalidation;
- automatic refetch.

### Реализованный вертикальный срез

`Chats`:

```text
POST /chats
GET  /chats
GET  /chats/{id}
```

Полная цепочка:

```text
React UI
   |
   v
TanStack Query
   |
   v
 HTTP
   |
   v
ASP.NET Core
   |
   v
Chats Module
   |
   v
EF Core
   |
   v
PostgreSQL
```

После создания нового Chat TanStack Query инвалидирует кэш и повторно загружает данные из API.

## 🧩 Backend Architecture

Core API развивается как **Modular Monolith**.

Целевые модули:

```text
Core API
├── Identity
├── Chats
├── Documents
├── Authorization
├── Audit
└── Integrations
```

Подход позволяет сохранять чёткие domain boundaries без преждевременного перехода к распределённой микросервисной архитектуре.

## 🤖 AI Architecture

AI функциональность не встраивается напрямую в Core API.

Для неё предусмотрены отдельные компоненты:

```text
AI Orchestrator
LLM Gateway
Ingestion Worker
```

Назначение:

- RAG orchestration;
- embeddings;
- document ingestion;
- LLM routing;
- controlled tool execution;
- data classification;
- policy enforcement перед отправкой данных во внешнюю LLM.

Python используется для AI workloads, .NET для Core API и основной бизнес логики.

## 🗄️ Data Layer

Основное хранилище:

```text
PostgreSQL + pgvector
```

Базовый подход:

```text
EF Core  основной доступ к данным
Dapper   точечно, где это оправдано
```

PostgreSQL остаётся source of truth для persistent application state.

RabbitMQ используется для фоновых задач, но не является источником истины.

## 🔐 Security Boundaries

Ключевые правила:

- authorization выполняется до передачи защищённых данных в LLM;
- LLM не является security authority;
- доступ к внешним LLM проходит через LLM Gateway;
- document access контролируется на уровне платформы;
- audit trail является частью базовой архитектуры;
- Identity строится вокруг OIDC / OAuth2;
- роли MVP: `USER`, `ADMIN`, `SECURITY_ADMIN`.

## 🔭 Observability и HTTP Infrastructure

В Core API реализована базовая cross-cutting инфраструктура:

```text
HTTP Request
    |
    v
CorrelationIdMiddleware
    |
    v
RequestLoggingMiddleware
    |
    v
ExceptionHandler
    |
    v
ProblemDetails
    |
    v
  CORS
    |
    v
Endpoint
```

Health check учитывает состояние PostgreSQL:

```text
Core API + PostgreSQL доступны -> 200 Healthy
PostgreSQL недоступен          -> 503 Unhealthy
```

## 🧪 Verification

Backend:

- xUnit;
- WebApplicationFactory;
- HTTP integration tests;
- validation / 404 / Correlation ID coverage.

Frontend:

- Vitest;
- Testing Library;
- query loading test;
- mutation + refetch test.

Единая verification команда:

```powershell
.\scripts\verify.ps1
```

На текущем этапе:

```text
Backend tests:  6
Frontend tests: 2
```

## 🚫 Что сознательно не добавлено на этом этапе

Пока не используются:

- Kubernetes;
- service mesh;
- полноценный микросервисный ландшафт;
- agent framework;
- distributed orchestration;
- multi-provider LLM routing;
- Local LLM GPU cluster.

Это осознанное архитектурное решение: сначала стабильная Full Stack и platform foundation, затем дополнительная распределённость и AI-сложность только при наличии реальной необходимости.

## ⚙️ Технологический стек

### Backend

- .NET 10
- ASP.NET Core
- EF Core
- Npgsql
- PostgreSQL
- pgvector

### Frontend

- React
- TypeScript
- Vite
- TanStack Query

### AI

- Python
- FastAPI

### Infrastructure

- Docker Compose
- RabbitMQ
- Object Storage
- OIDC / Keycloak
- External LLM Provider

### Testing

- xUnit
- WebApplicationFactory
- Vitest
- Testing Library

## 📁 Репозиторий

```text
enterprise-genai-platform/
├── docs/
├── scripts/
├── src/
│   ├── core-api/
│   └── web/
├── tests/
├── compose.yaml
├── EnterpriseGenAIPlatform.slnx
├── global.json
└── README.md
```

## 📚 Документация

Архитектурные материалы и ADR находятся в `docs/`.

Подробное описание результата Stage 1:

```text
docs/stage-1-full-stack-mvp.md
```

Инструкции по локальному запуску и воспроизведению окружения вынесены в документацию Stage 1, чтобы README оставался архитектурным обзором проекта, а не пошаговой инструкцией.

## 🎯 Текущая архитектурная точка

Сейчас сформирована рабочая база, на которой можно последовательно реализовывать продуктовые модули и AI функциональность без пересмотра фундаментальных решений Stage 0.

Текущая платформа уже содержит:

```text
React
  |
  v
ASP.NET Core
  |
  v
PostgreSQL
```

с тестируемой Full Stack вертикалью, модульным Core API, инфраструктурным HTTP pipeline и подготовленным контуром для дальнейшего подключения Documents, RAG, LLM Gateway и остальных enterprise функций.
