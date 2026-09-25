# ADR-005 — Background Job Mechanism (Механизм фоновых задач)

Status: Accepted

Date: 2026-09-24

## Context

Enterprise GenAI Platform требует выполнения
длительных и фоновых операций.

Основные сценарии:

- document ingestion;
- extraction;
- chunking;
- embeddings generation;
- indexing;
- повторная обработка документов;
- фоновые integration jobs.

HTTP request не должен оставаться открытым
на всё время выполнения таких операций.

Необходимо обеспечить:

- at-least-once delivery (доставка как минимум один раз);
- acknowledgement (подтверждение) обработки;
- retry;
- обработку временных ошибок;
- возможность обработки failed jobs (неудачные задания);
- горизонтальное масштабирование workers;
- защиту от повторного выполнения через idempotency.

Текущая проектная нагрузка не требует
event streaming уровня миллионов сообщений в секунду
или длительного replay полного event log.

## Decision (Решение)

Использовать RabbitMQ как механизм доставки
background jobs (фоновые задачи) между сервисами и workers.

Использовать модель at-least-once delivery.

Consumers обязаны быть idempotent.

Для операций, где создание сообщения должно быть атомарно
с изменением business data в PostgreSQL,
использовать Transactional Outbox Pattern (Шаблон «Транзакционная исходящая очередь»).

Kafka не используется на текущем этапе.

Возможность перехода или добавления Kafka
будет пересмотрена при появлении требований
к event streaming, длительному replay,
большому количеству независимых consumers
или существенно более высокой event-нагрузке.

## Alternatives Considered (Рассмотренные альтернативы)

### Alternative A — PostgreSQL Job Table

Плюсы:

- минимум дополнительной инфраструктуры;
- возможность создавать job в одной транзакции с business data;
- простая эксплуатация;
- хорошо подходит для умеренных объёмов фоновой обработки.

Минусы:

- queue semantics необходимо реализовывать самостоятельно;
- retry, visibility timeout и failed jobs требуют собственного кода;
- дополнительная нагрузка на основную БД;
- хуже разделяет transactional storage и messaging workload.

### Alternative B — RabbitMQ

Плюсы:

- хорошо подходит для task queues;
- acknowledgement (подтверждение);
- retry patterns;
- dead-letter queues (очереди недоставленных сообщений);
- routing;
- consumer competing (конкуренция потребителей);
- горизонтальное масштабирование workers;
- существенно проще Kafka для текущего сценария.

Минусы:

- дополнительный infrastructure component;
- требует monitoring и administration;
- не предназначен для длительного хранения event history;
- replay старых событий не является его основной моделью.

### Alternative C — Kafka

Плюсы:

- высокая пропускная способность;
- распределённая архитектура;
- partition ordering;
- event log;
- replay;
- multiple independent consumer groups;
- хорошо подходит для event streaming.

Минусы:

- выше operational complexity;
- сложнее локальная разработка и эксплуатация;
- избыточна для обычной task queue;
- текущие требования не используют большую часть её преимуществ.

## Consequences (Последствия)

### Positive

- Document Ingestion Worker можно масштабировать независимо;
- тяжёлые задачи не блокируют HTTP API;
- встроенная модель acknowledgement;
- удобная организация retry и failed messages;
- более простая инфраструктура, чем Kafka;
- архитектура остаётся готовой к последующему появлению Kafka,
  если возникнут соответствующие требования.

### Negative

- появляется дополнительный infrastructure component;
- необходимо мониторить RabbitMQ;
- consumers должны поддерживать idempotency;
- появляются asynchronous consistency (асинхронная согласованность) и дополнительные failure scenarios (сценарии отказа);
- Transactional Outbox (Транзакционная исходящая очередь) требует отдельного publisher process (процесс издателя).

## Related Requirements (Связанные требования)

- Availability;
- Recoverability;
- Scalability;
- отсутствие потери принятых background jobs;
- независимое масштабирование workers.

## Related ADR (Связанные ADR)

- ADR-003 — Application Architecture;
- ADR-004 — Sync and Async Communication.
