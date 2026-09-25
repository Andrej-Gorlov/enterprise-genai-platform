# ADR-004 — Use synchronous and asynchronous communication based on workload type

Status: Accepted

Date: 2026-09-24

## Context

Enterprise GenAI Platform содержит разные типы операций.

Часть операций является интерактивной:

- получение данных пользователем;
- отправка AI-вопроса;
- authorization;
- вызов LLM;
- выполнение read-only business API request.

Пользователь ожидает непосредственный результат.

Другие операции могут выполняться долго
или не требуют мгновенного результата:

- document ingestion;
- text extraction;
- chunking;
- embeddings generation;
- indexing;
- audit delivery;
- background integrations;
- retry failed operations.

Использование только synchronous communication
для всех сценариев может привести к:

- длинным HTTP connections;
- timeout;
- плохой изоляции отказов;
- сильной связанности компонентов.

Использование только asynchronous communication
для всех сценариев усложнит простые request/response workflows.

## Decision (Решение)

Использовать synchronous communication
для интерактивных операций,
где вызывающий компонент непосредственно ожидает результат.

Основной synchronous transport:

- HTTP/HTTPS;
- JSON API.

Для streaming AI-response
в дальнейшем может использоваться SSE
или другой подходящий streaming transport.

Использовать asynchronous communication
для:

- long-running operations;
- background jobs;
- retryable processing;
- document ingestion;
- audit/event delivery;
- операций, результат которых не требуется
  в рамках текущего HTTP request.

Конкретный механизм background messaging
определяется ADR-005.

Synchronous и asynchronous взаимодействие
не должны использоваться произвольно.

Выбор должен зависеть
от характера операции и требований к failure handling (обработка сбоев).

## Alternatives Considered (Рассмотренные альтернативы)

### Alternative A — Everything Synchronous (Всё синхронно)

Все взаимодействия выполняются
через HTTP request/response.

Плюсы:

- простая модель;
- проще debugging;
- меньше infrastructure components;
- легко отслеживать последовательность вызовов.

Минусы:

- long-running operations (длительные операции) блокируют request;
- повышается вероятность timeout;
- сложнее выполнять retry;
- временный отказ downstream component (компонент последующего этапа)
  напрямую влияет на вызывающий сервис;
- плохо подходит для document ingestion (загрузка документов) и background processing (фоновая обработка).

### Alternative B — Everything Asynchronous

Все взаимодействия выполняются через message broker.

Плюсы:

- хорошая decoupling;
- естественный retry;
- удобная обработка фоновых workloads;
- сервисы меньше зависят от времени ответа друг друга.

Минусы:

- значительно сложнее архитектура;
- сложнее request/response пользовательские сценарии;
- eventual consistency появляется даже там, где она не нужна;
- усложняется debugging;
- сложнее error propagation (распространение ошибок) пользователю;
- требуется корреляция сообщений.

### Alternative C — Hybrid Communication Model

Использовать synchronous и asynchronous communication
в зависимости от сценария.

Плюсы:

- интерактивные операции остаются простыми;
- long-running задачи не блокируют HTTP;
- можно независимо масштабировать workers;
- failure model (модель отказа) выбирается в соответствии с задачей.

Минусы:

- необходимо поддерживать две модели коммуникации;
- сложнее tracing;
- разработчики должны понимать, когда использовать каждую модель;
- asynchronous workflows требуют idempotency и управления состояниями задач.

## Consequences (Последствия)

### Positive

- пользовательские сценарии остаются понятными;
- background processing отделён от HTTP lifecycle;
- document ingestion не блокирует Core API;
- временные ошибки background jobs можно обрабатывать retry;
- можно независимо масштабировать workers;
- система лучше изолирует часть отказов.

### Negative

- появляется distributed communication (распределенная коммуникация);
- необходимо внедрить correlation ID;
- asynchronous operations требуют статусов PENDING / PROCESSING / COMPLETED / FAILED;
- требуется idempotency;
- сложнее end-to-end tracing (сквозная трассировка);
- возможна eventual consistency (согласованность в конечном счете) между компонентами.

## Related Requirements (Связанные требования)

- Performance;
- Availability;
- Scalability;
- Recoverability;
- AI response P95;
- long-running document processing;
- отсутствие потери background jobs.

## Related ADR (Связанный ADR)

- ADR-003 — Application Architecture;
- ADR-005 — Background Job Mechanism;
- ADR-006 — LLM Routing and Data Classification.
