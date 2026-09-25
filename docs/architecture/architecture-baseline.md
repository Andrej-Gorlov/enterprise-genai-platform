# Architecture Baseline

Version: 1.0

Status: Accepted

## 1. Purpose (Цель)

Документ фиксирует исходную архитектурную конфигурацию
Enterprise GenAI Platform перед началом реализации.

Изменение значимых архитектурных решений должно
фиксироваться через ADR.

## 2. Application Architecture

Core business backend реализуется как Modular Monolith
на .NET 10 / ASP.NET Core.

Внутри Core API выделяются логические модули:

- Identity;
- Chats;
- Documents;
- Authorization;
- Audit;
- Integrations.

Выделение этих модулей в отдельные микросервисы
на этапе MVP не планируется.

Специализированные workloads могут иметь
отдельный deployment, если отличаются:

- технологическим стеком;
- характером нагрузки;
- масштабированием;
- failure model;
- lifecycle.

## 3. Application Containers

### Web Application

Technology:

- React;
- TypeScript;
- Vite.

Responsibility:

Пользовательский интерфейс платформы.

### Core API

Technology:

- .NET 10;
- ASP.NET Core.

Responsibility:

- business logic;
- authentication / authorization;
- chats;
- documents;
- audit;
- platform API;
- orchestration прикладных операций.

### AI Orchestrator

Technology:

- Python;
- FastAPI.

Responsibility:

- RAG pipeline;
- retrieval;
- context construction;
- AI workflows.

### Document Ingestion Worker

Technology:

- Python.

Responsibility:

Асинхронная обработка документов:

extract
→ clean
→ chunk
→ embeddings
→ indexing.

### LLM Gateway

Technology:

- .NET 10;
- ASP.NET Core.

Responsibility:

Единая точка доступа к LLM.

На этапе MVP используется один внешний LLM provider.

## 4. Data and Storage

### Platform Database

Technology:

- PostgreSQL;
- pgvector.

Используется для:

- пользователей;
- chats;
- messages;
- documents metadata;
- ACL;
- audit metadata;
- chunks;
- embeddings.

### Object Storage

Technology:

S3-compatible Object Storage.

Для локальной разработки:

- MinIO.

Используется для хранения оригинальных документов
и других бинарных файлов.

## 5. Authentication and Authorization

Для authentication используется OIDC-compatible
Identity Provider.

Для локальной разработки предполагается Keycloak.

Роли MVP:

- USER;
- ADMIN;
- SECURITY_ADMIN.

Доступ к документам:

- OWNER;
- SHARED.

Проверка доступа должна выполняться до передачи
защищённого содержимого в LLM.

## 6. AI and LLM

Первым внешним LLM provider используется OpenAI.

AI Orchestrator не должен напрямую зависеть
от конкретного LLM provider.

Все обращения к LLM выполняются через LLM Gateway.

Local LLM и multi-provider routing
не входят в первую версию MVP.

## 7. Asynchronous Processing

Для фоновой обработки документов используется RabbitMQ.

Core API публикует ingestion job.

Document Ingestion Worker получает и обрабатывает job.

RabbitMQ отвечает за доставку работы,
но не является source of truth состояния документа.

Состояние документа хранится в PostgreSQL.

Пример:

UPLOADED
→ QUEUED
→ PROCESSING
→ READY

или:

PROCESSING
→ FAILED.

## 8. Communication Model

Синхронное взаимодействие используется
для пользовательских request/response операций.

Асинхронное взаимодействие используется
для long-running background workloads,
в частности document ingestion.

## 9. Security Baseline

Применяются следующие принципы:

- Deny by Default (Запрет по умолчанию);
- Least Privilege (Принцип минимальных привилегий);
- Defense in Depth (Эшелонированная оборона);
- Fail Secure (Безопасный);
- Authorization Before Data Exposure (Авторизация перед раскрытием данных);
- Authorization at the Point of Action (Авторизация в момент выполнения действия);
- LLM Is Not a Security Authority (LLM не является авторитетным источником в вопросах безопасности.);
- Retrieved Content Is Untrusted (Полученным данным нельзя доверять).

## 10. MVP Deployment

На этапе MVP локальное окружение разворачивается
с помощью Docker Compose.

Kubernetes и Helm не входят в MVP.

## 11. Related ADR

- ADR-001 — .NET and Python;
- ADR-002 — PostgreSQL and pgvector;
- ADR-003 — Application Boundaries;
- ADR-004 — Synchronous and Asynchronous Communication;
- ADR-005 — Background Job Mechanism;
- ADR-006 — LLM Routing and Data Classification.

## 12. MVP Boundary

Функциональные границы первой версии определены в:

docs/requirements/mvp-scope.md
