# MVP Scope

## 1. MVP Goal

Первая версия должна подтвердить возможность построения
безопасной корпоративной GenAI-платформы,
которая отвечает на вопросы пользователей
на основании разрешённых корпоративных документов.

## 2. Included

### Authentication

Пользователь может войти в систему.

Для MVP используется OIDC-compatible Identity Provider.

Платформа получает стабильный user identity.

### Roles

Поддерживаются роли:

- USER;
- ADMIN;
- SECURITY_ADMIN.

USER использует AI и доступные ему документы.

ADMIN управляет платформой.

SECURITY_ADMIN получает доступ к security/audit-информации
для анализа событий безопасности.

### Documents

Пользователь может:

- загрузить документ;
- увидеть состояние обработки;
- просмотреть список доступных ему документов;
- удалить собственный документ.

Поддерживаемые форматы MVP:

- PDF;
- TXT;
- MD.

Другие форматы добавляются после MVP.

### Document Access

Для MVP используются:

- OWNER;
- SHARED.

Пользователь не должен иметь возможность:

- получить документ;
- получить chunks документа;
- использовать документ через RAG,

если у него нет доступа.

Попытки обращения к запрещённым данным
должны фиксироваться в security/audit trail.

### Document Ingestion

После загрузки документа система должна:

1. сохранить оригинальный файл;
2. создать ingestion job (задача по загрузке данных);
3. отправить job в RabbitMQ;
4. извлечь текст;
5. разбить текст на chunks;
6. создать embeddings;
7. сохранить chunks и embeddings;
8. перевести документ в READY.

При ошибке:

Document → FAILED.

### Background Processing (Фоновая обработка)

Для фоновой обработки документов используется RabbitMQ.

Document Ingestion Worker должен обрабатывать
задачи независимо от HTTP request lifecycle.

Повторная доставка одного сообщения
не должна приводить к повреждению состояния документа.

### RAG

Пользователь может задать вопрос.

Система должна:

1. сохранить сообщение пользователя;
2. создать AI Request;
3. выполнить semantic retrieval;
4. применить document access filtering;
5. выбрать релевантные chunks;
6. сформировать context;
7. вызвать LLM через LLM Gateway;
8. сохранить ответ;
9. вернуть ответ пользователю.

### Sources

AI-ответ должен содержать ссылки
на использованные источники.

Для каждого источника MVP достаточно отображать:

- document name;
- page/chunk reference;
- релевантный фрагмент текста.

### Security Rule

Authorization должна применяться до передачи
retrieved content в LLM.

Frontend не является security boundary,
на которой можно полагаться для authorization.

LLM не принимает окончательные решения
о правах пользователя.

### Chats

Пользователь может:

- создать чат;
- отправить сообщение;
- получить AI-ответ;
- открыть историю чата;
- повторить неудавшийся AI-запрос.

История хранится в PostgreSQL.

### Security Audit

MVP должен фиксировать как минимум:

- user;
- timestamp;
- action;
- resource;
- ALLOW / DENY;
- reason;
- correlation ID.

SECURITY_ADMIN должен иметь возможность
просматривать эти события.

Интеграция с внешним SIEM в MVP не требуется.

### LLM Gateway

LLM Gateway предоставляет единый API
для обращения к LLM.

MVP поддерживает:

- один LLM provider;
- timeout;
- basic retry;
- model configuration;
- token usage collection.

Первый provider:

- OpenAI.

Multi-provider routing и automatic fallback (Маршрутизация между несколькими провайдерами и автоматическое резервирование)
не входят в MVP.

## 3. Out of Scope

Следующее не является условием завершения MVP:

- AI Agents;
- Agent Tool Execution;
- Corporate Business Systems integrations;

- Local LLM;
- vLLM;
- GPU infrastructure;

- multi-provider routing;
- automatic LLM fallback;

- Redis;
- Kafka;

- Kubernetes;
- Helm;

- Terraform / OpenTofu;
- Ansible;

- Prometheus;
- Grafana;
- full OpenTelemetry infrastructure;

- external SIEM integration;

- ABAC;
- DLP;

- advanced prompt-injection protection;

- hybrid search;
- reranker;
- RAG evaluation framework;

- multi-tenancy;

- high availability;
- autoscaling.

## 4. MVP Decisions

### Document Formats

Decision:

PDF + TXT + MD.

Reason:

Этих форматов достаточно для проверки полного
document ingestion и RAG pipeline.

Другие форматы будут добавляться
после подтверждения работоспособности MVP.

### Roles

Decision:

USER / ADMIN / SECURITY_ADMIN.

Reason:

Для MVP необходимо разделить:

- обычное использование платформы;
- администрирование;
- контроль безопасности.

### Document Access

Decision:

OWNER + SHARED.

Reason:

Разграничение доступа является частью
основной идеи Enterprise GenAI Platform.

Необходимо уже в MVP доказать,
что пользователь без доступа к документу
не сможет получить его содержимое
ни напрямую, ни через RAG.

### External LLM Provider

Decision:

OpenAI.

Reason:

OpenAI используется как первый внешний LLM provider,
поскольку он знаком команде и позволяет быстрее
проверить архитектуру и RAG pipeline.

AI-компоненты взаимодействуют с ним
через LLM Gateway.

### Background Jobs

Decision:

RabbitMQ.

Reason:

Document ingestion является асинхронной фоновой задачей.

RabbitMQ соответствует модели:

producer
→ queue
→ worker
→ acknowledgement.

Kafka на этапе MVP не используется,
поскольку текущие требования не требуют:

- event streaming;
- длительного replay событий;
- большого количества независимых consumer groups;
- очень высокого event throughput.

Необходимость Kafka будет рассмотрена повторно,
если соответствующие требования появятся.

## 5. MVP Definition of Done (Определение готовности)

MVP считается завершённым, если:

1. User A может войти в систему.

2. User A может загрузить PDF, TXT или MD документ.

3. Документ проходит состояния:

   UPLOADED
   → QUEUED
   → PROCESSING
   → READY.

4. Пользователь задаёт вопрос,
   ответ на который содержится в документе.

5. RAG находит релевантные chunks.

6. LLM формирует ответ.

7. Пользователь получает:
   - ответ;
   - источник;
   - document reference;
   - page/chunk reference.

8. Вопрос и ответ сохраняются в истории чата.

9. User B без доступа к документу:
   - не может получить файл;
   - не может получить его chunks;
   - не может получить информацию из него через RAG.

10. Попытка запрещённого доступа фиксируется в audit.

11. SECURITY_ADMIN может увидеть security event.

12. При временной ошибке LLM пользовательское сообщение
    не теряется.

13. При сбое Document Ingestion Worker
    документ не теряется,
    а задача может быть обработана повторно.
