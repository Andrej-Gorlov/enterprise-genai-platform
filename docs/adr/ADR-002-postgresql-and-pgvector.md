# ADR-002 — Use PostgreSQL and pgvector for persistent and vector data

Status: Accepted

Date: 2026-09-24

## Context

Enterprise GenAI Platform должна хранить несколько типов данных:

- users;
- chats;
- messages;
- document metadata;
- document ACL;
- audit metadata;
- chunks;
- embeddings.

Для RAG необходим vector search по embeddings.

Необходимо выбрать способ хранения,
который позволит:

- хранить relational business data;
- выполнять vector similarity search;
- применять metadata filtering;
- учитывать document ACL;
- поддерживать backup и recovery;
- не усложнять инфраструктуру раньше времени.

Рассматриваются два основных подхода:

1. использовать PostgreSQL для business data
   и специализированную vector database отдельно;

2. использовать PostgreSQL совместно с pgvector
   для business data и vector search.

Начальный проектный масштаб:

- до 1 000 000 документов;
- до 10 000 000 chunks.

Точный профиль нагрузки пока неизвестен
и будет уточняться по результатам benchmarks
и реальной эксплуатации.

## Decision (Решение)

Использовать PostgreSQL как основной persistent source of truth (постоянный источник достоверных данных).

Использовать расширение pgvector
для хранения embeddings и выполнения vector search.

На начальном этапе не вводить
отдельную специализированную vector database.

Document metadata, ACL, chunks и embeddings
должны оставаться логически связанными,
чтобы retrieval мог применять security filtering
до передачи данных в LLM.

Переход на специализированную vector database
должен рассматриваться только при наличии измеримых причин,
например:

- pgvector не обеспечивает требуемый latency (задержка);
- недостаточна производительность при целевом объёме данных;
- требуется независимое масштабирование vector search;
- специализированная БД предоставляет необходимые возможности,
  которых недостаточно в PostgreSQL.

## Alternatives Considered (Рассмотренные альтернативы)

### Alternative A — PostgreSQL + pgvector

Плюсы:

- один основной datastore;
- меньше infrastructure components;
- проще backup и recovery;
- relational data и vector data находятся рядом;
- удобно применять metadata filtering;
- проще связывать vector results с document ACL;
- PostgreSQL хорошо знаком enterprise-разработчикам;
- ниже operational complexity.

Минусы:

- vector workload создаёт дополнительную нагрузку на PostgreSQL;
- специализированные vector databases могут лучше масштабироваться
  для тяжёлых ANN workloads;
- необходимо правильно настраивать индексы и запросы;
- при росте нагрузки может потребоваться разделение workloads.

### Alternative B — PostgreSQL + Qdrant

Плюсы:

- специализированный vector search;
- отдельное масштабирование vector workload;
- развитые возможности metadata filtering;
- оптимизация под vector retrieval.

Минусы:

- появляется второй datastore;
- сложнее consistency между PostgreSQL и Qdrant;
- сложнее backup / restore;
- необходимо синхронизировать document lifecycle и vector data;
- выше operational complexity.

### Alternative C — PostgreSQL + Weaviate

Плюсы:

- специализированная vector database;
- дополнительные AI-oriented возможности;
- независимое масштабирование.

Минусы:

- дополнительная инфраструктура;
- усложнение эксплуатации;
- необходимость синхронизации с основной relational database;
- часть возможностей может быть избыточна для первой версии.

### Alternative D — PostgreSQL + OpenSearch / Elasticsearch

Плюсы:

- мощный full-text search;
- хорошо подходит для hybrid search;
- развитая search ecosystem.

Минусы:

- дополнительный сложный infrastructure component;
- отдельное хранение и синхронизация данных;
- выше требования к эксплуатации;
- избыточно до появления требований,
  оправдывающих отдельный search cluster.

## Consequences (Последствия)

### Positive

- архитектура первой версии остаётся проще;
- уменьшается количество infrastructure components;
- ACL, metadata и embeddings можно использовать
  в рамках одного data platform;
- проще обеспечить consistency;
- проще backup и recovery;
- pgvector позволяет начать разработку RAG
  без отдельной vector database;
- сохраняется возможность миграции позже.

### Negative

- PostgreSQL получает дополнительный vector workload;
- потребуется benchmark vector search;
- при росте нагрузки могут понадобиться
  отдельные read replicas или специализированный vector store;
- возможная будущая миграция потребует изменения retrieval layer;
- необходимо следить за размером индексов и производительностью запросов.

## Related Requirements (Связанные требования)

- Performance;
- Scalability;
- Security;
- Recoverability;
- document-level access control;
- RAG retrieval;
- хранение до 10 000 000 chunks.

## Related ADR (Связанные ADR)

- ADR-003 — Application Architecture;
- ADR-004 — Sync and Async Communication;
- ADR-005 — Background Job Mechanism.
