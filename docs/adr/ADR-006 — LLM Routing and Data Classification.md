# ADR-006 — LLM Routing and Data Classification

Status: Accepted

Date: 2026-09-24

## Context

Enterprise GenAI Platform должна поддерживать
как локальные, так и внешние LLM.

Использование внешних LLM может быть полезно
для получения более высокого качества,
доступа к специализированным моделям или резервирования.

Одновременно корпоративные запросы могут содержать:

- public data;
- internal data;
- confidential data;
- данные с дополнительными ограничениями организации.

Передача таких данных во внешний LLM provider
может быть запрещена политиками безопасности.

Также необходимо определить,
что происходит при недоступности основной модели.

Простой fallback:

Local LLM unavailable → send request to External LLM

является неприемлемым,
если внешнему provider запрещено получать
данный класс корпоративных данных.

## Decision (Решение)

Все обращения к LLM должны выполняться
через LLM Gateway.

AI services не должны напрямую интегрироваться
с конкретными LLM providers.

Каждый LLM request должен содержать
необходимую metadata для применения routing policy,
включая data classification.

Начальная классификация:

- PUBLIC;
- INTERNAL;
- CONFIDENTIAL.

LLM Gateway должен выбирать модель
на основании:

- data classification;
- разрешённых providers;
- availability модели;
- model capabilities;
- политики организации.

Пример базовой routing policy:

PUBLIC:
может обрабатываться локальной
или разрешённой внешней LLM.

INTERNAL:
обрабатывается согласно политике организации.

CONFIDENTIAL:
обрабатывается локальной LLM
или только явно одобренным корпоративным provider.

Fallback не должен снижать уровень безопасности.

Если допустимая fallback-модель отсутствует,
запрос завершается контролируемой ошибкой.

Security policy имеет приоритет
над availability.

LLM Gateway также отвечает за:

- provider abstraction;
- timeout;
- retry;
- fallback;
- rate limiting;
- token accounting;
- model usage metadata;
- routing policies.

LLM Gateway не определяет,
имеет ли пользователь доступ к документу.

User/document authorization выполняется
до передачи данных в LLM layer.

## Alternatives Considered (Рассмотренные альтернативы)

### Alternative A — Direct Provider Access (Прямой доступ к поставщику услуг)

Каждый AI service самостоятельно обращается
к OpenAI, локальной LLM или другим providers.

Плюсы:

- проще первая реализация;
- меньше сетевых hops;
- сервис напрямую использует нужный provider.

Минусы:

- provider logic дублируется;
- сложнее контролировать data leakage;
- разные сервисы могут применять разные security rules;
- сложнее fallback;
- сложнее централизованно учитывать tokens и usage;
- сильная связанность AI services с конкретными providers.

### Alternative B — Always Use External LLM

Все запросы отправляются внешнему provider.

Плюсы:

- простая инфраструктура;
- не требуется local inference infrastructure;
- можно использовать мощные hosted models.

Минусы:

- риск передачи корпоративных данных наружу;
- зависимость от внешнего provider;
- возможные compliance ограничения;
- стоимость;
- отсутствие полного контроля над inference environment.

### Alternative C — Always Use Local LLM

Все запросы выполняются локально.

Плюсы:

- данные остаются внутри инфраструктуры организации;
- больше контроля;
- проще соблюдать строгую data policy.

Минусы:

- требуется GPU infrastructure;
- высокая стоимость эксплуатации;
- локальная модель может уступать внешней по качеству;
- сложнее масштабирование;
- необходимо самостоятельно обслуживать inference stack.

### Alternative D — Policy-Based LLM Gateway (Шлюз для LLM на основе политик)

Все обращения проходят через LLM Gateway,
который выбирает provider
на основании data classification и policy.

Плюсы:

- централизованный security control;
- единый provider abstraction;
- controlled fallback;
- возможность использовать local и external models;
- централизованный monitoring и usage accounting;
- AI services не зависят от конкретного provider.

Минусы:

- дополнительный сервис;
- дополнительный network hop;
- Gateway становится важным infrastructure component;
- требуется высокая доступность Gateway;
- routing policies (политики маршрутизации) необходимо поддерживать и тестировать.

## Consequences (Последствия)

### Positive

- снижается риск неконтролируемой передачи данных;
- правила работы с LLM централизованы;
- можно использовать разные модели для разных типов запросов;
- fallback становится policy-aware;
- AI services не зависят напрямую от providers;
- упрощается централизованный token и usage accounting;
- можно постепенно добавлять новых providers.

### Negative

- LLM Gateway становится критическим компонентом;
- routing policy усложняет систему;
- необходима data classification;
- ошибки классификации могут привести
  к неправильному routing;
- потребуется monitoring availability (доступность) моделей;
- потребуется тестирование fallback policy;
- дополнительный network hop увеличивает latency (задержка).

## Related Requirements (Связанные требования)

- Security;
- Availability;
- Data Minimization;
- Fail Secure (Безопасный);
- невозможность передачи запрещённых данных внешнему provider;
- поддержка Local и External LLM;
- controlled fallback.

## Related ADR (Связанный ADR)

- ADR-001 — Use .NET 10 and Python;
- ADR-003 — Application Architecture;
- ADR-004 — Sync and Async Communication.
