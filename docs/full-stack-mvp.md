# Full Stack MVP

## Статус
Завершён.

Этап 1 фиксирует реализацию первой рабочей Full Stack-вертикали Enterprise GenAI Platform и создаёт техническую основу для дальнейшего развития функциональных модулей платформы:

React + TypeScript  
-> ASP.NET Core  
-> EF Core  
-> PostgreSQL.

Core API реализован как модульный монолит. Frontend отделён от Backend через HTTP API, а PostgreSQL является основным хранилищем постоянного состояния приложения.

## Реализовано

### Backend

- ASP.NET Core на .NET 10.
- Dependency Injection и конвейер middleware.
- Типизированная конфигурация с использованием Options pattern и проверкой при запуске.
- Интеграция с PostgreSQL через Npgsql и EF Core.
- Миграции EF Core.
- Подключено расширение pgvector.
- Реализован первый вертикальный срез модульного монолита, модуль Chats.
- REST endpoints:
  - `POST /chats`
  - `GET /chats`
  - `GET /chats/{id}`
- Валидация запросов.
- Ответы об ошибках на основе ProblemDetails.
- CORS политика для локального React приложения.
- Middleware для Correlation ID.
- Структурированное логирование HTTP запросов.
- Health check с проверкой PostgreSQL.

### Frontend

- React + TypeScript + Vite.
- Конфигурация Core API через переменные окружения.
- Выделен отдельный слой взаимодействия с Core API.  
- Server state управляется через TanStack Query.  
- Операции чтения реализованы через Query.  
- Операции изменения состояния реализованы через Mutation.  
- После изменения данных выполняется инвалидация cache и автоматический refetch.

### Тестирование

Backend:

- xUnit.
- Модульный тест доменной модели.
- Интеграционные тесты через `WebApplicationFactory`.
- Изолированная EF Core InMemory база используется для проверки HTTP контрактов без зависимости от локального PostgreSQL.
- Покрыты сценарии:
  - пустой список чатов;
  - создание чата;
  - получение чата после создания;
  - ошибка валидации;
  - ответ Not Found;
  - передача Correlation ID.

PostgreSQL специфичное поведение, migrations и relational constraints не подменяются этими тестами и проверяются отдельно в рамках инфраструктурной и End-to-End приёмки.

Frontend:

- Vitest.
- Testing Library.
- jsdom.
- Тест загрузки чатов.
- Тест создания чата и инвалидации Query с последующей повторной загрузкой.

### Automated Verification

Для воспроизводимой проверки состояния репозитория используется единый verification script:

```powershell
.\scripts\verify.ps1
```

Скрипт проверяет:

1. Сборку .NET solution.
2. Автоматические тесты Backend.
3. ESLint для Frontend.
4. Автоматические тесты Frontend.
5. Production сборку Frontend.

## Локальная разработка

### Требования

- .NET SDK 10.
- Node.js и npm.
- Docker с Docker Compose.

### Конфигурация

Создайте локальный конфигурационный файл инфраструктуры:

```powershell
Copy-Item .\.env.example .\.env
```

Укажите локальный пароль PostgreSQL в `.env`.

Создайте конфигурацию Frontend для среды разработки:

```powershell
Copy-Item .\src\web\.env.example .\src\web\.env.development
```

API по умолчанию доступен локально по адресу:

```text
http://localhost:5091
```

### Запуск PostgreSQL

```powershell
docker compose up -d postgres
docker compose ps
```

### Восстановление локальных .NET tools

```powershell
dotnet tool restore
```

### Применение миграций базы данных

```powershell
dotnet tool run dotnet-ef database update `
  --project .\src\core-api\EnterpriseGenAI.Core.Api.csproj
```

### Запуск Core API

```powershell
dotnet run --project .\src\core-api\EnterpriseGenAI.Core.Api.csproj
```

Core API:

```text
http://localhost:5091
```

Health endpoint:

```text
http://localhost:5091/health
```

### Запуск Web приложения

```powershell
cd .\src\web
npm install
npm run dev
```

Web приложение:

```text
http://localhost:5173
```

## Сквозная приёмка

Сценарий приёмки Этапа 1 успешно проверен:

1. PostgreSQL запускается и сообщает состояние `healthy`.
2. EF Core сообщает, что схема базы данных актуальна.
3. Core API `/health` возвращает `200 Healthy`.
4. React загружает существующие чаты из Core API.
5. Чат можно создать через интерфейс React.
6. TanStack Query инвалидирует и повторно загружает список чатов.
7. Новый чат сохраняется после обновления страницы браузера.
8. Запись существует непосредственно в PostgreSQL.
9. REST API возвращает сохранённую запись.
10. Correlation ID передаётся через HTTP конвейер.
11. Автоматическая проверка всего репозитория завершается успешно.

## Текущая архитектурная граница

Этап 1 ограничен созданием базовой Full Stack вертикали и инфраструктурной основы приложения.  

Фактически реализованный функциональный срез на текущем этапе Chats. Архитектура Core API при этом предусматривает дальнейшее развитие модулей Identity, Projects, Documents, Authorization, Audit и Integrations.  

Core API сохраняет архитектуру модульного монолита. Переход к микросервисному разделению на данном этапе не требуется.  

AI специфичные workloads RAG, ingestion, AI Orchestrator и LLM Gateway остаются отдельными архитектурными компонентами и не входят в реализацию Этапа 1.

## Результат Этапа 1

По результатам Этапа 1 сформирована воспроизводимая и протестированная Full Stack база React -> ASP.NET Core -> PostgreSQL, подтверждённая автоматическими тестами и End-to-End приёмкой.  

Эта реализация становится базовым техническим слоем для последующего развития пользователей, проектов, документов, истории запросов, авторизации и AI/RAG возможностей без пересмотра архитектурных решений, принятых на Этапе 0.
