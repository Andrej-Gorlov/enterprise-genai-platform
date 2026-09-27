# C4 - Диаграмма контейнеров

## Enterprise GenAI Platform

Container Diagram показывает основные исполняемые приложения
и хранилища данных внутри Enterprise GenAI Platform.

![C4 Context Diagram](./container-diagram.png)

## Containers

### Web Application

Ответственность:
Предоставляет пользовательский интерфейс платформы:
чат, работу с документами, историю диалогов,
просмотр источников и административные функции.

Технологии:
React + TypeScript + Vite.

Зачем разделять:
Frontend имеет отдельную ответственность, жизненный цикл и развертывание.
Он не должен иметь прямого доступа к базам данных,
LLM или внутренним AI-компонентам.

Основные зависимости:

- Core API;
- Corporate Identity Provider в рамках authentication flow.

### Core API

Ответственность:
Предоставляет API для frontend, реализует бизнес-логику платформы,
управляет пользователями, чатами, документами,
authorization и координирует работу внутренних сервисов.

Технологии:
.NET 10 / ASP.NET Core.

Зачем разделять:
Является основной business/application boundary платформы.
Отделяет пользовательский интерфейс от AI,
хранилищ и корпоративных интеграций.

Основные зависимости:

- Platform Database;
- Object Storage;
- Redis;
- AI Orchestrator;
- Corporate Identity Provider;
- Corporate Business Systems;
- Corporate SIEM.

### AI Orchestrator

Ответственность:
Оркестрирует AI workflow.

Например:

retrieval
-> reranking
-> context construction
-> prompt construction
-> agent/tool execution
-> LLM request
-> processing result.

Технологии:
Python + FastAPI.

Зачем разделять:
AI компоненты имеют собственную Python/ML экосистему,
другой характер нагрузки и могут масштабироваться
независимо от основного backend.

Основные зависимости:

- LLM Gateway;
- Platform Database / pgvector;
- Redis;
- разрешённые tools/API.

### Document Ingestion Worker

Технологии:

.NET 10 Worker Service.

Ответственность:

- получение ingestion jobs из RabbitMQ;
- загрузка документов из Object Storage;
- извлечение текста;
- очистка;
- chunking;
- управление состоянием ingestion;
- сохранение результатов.

AI/ML-specific операции, требующие Python ecosystem,
делегируются Python AI-компонентам.

### LLM Gateway

Ответственность:
Предоставляет единый интерфейс обращения к LLM.

Отвечает за:

маршрутизацию моделей;
provider abstraction;
тайм-ауты;
повторные попытки;
fallback;
rate limiting;
token accounting;
policy enforcement.

Технологии:
.NET 10 / ASP.NET Core.

Зачем разделять:
Централизует доступ ко всем LLM и не позволяет
каждому AI сервису самостоятельно интегрироваться
с разными провайдерами.

Основные зависимости:

- Local LLM Inference;
- External LLM Provider.

### Platform Database

Ответственность:
Хранит долговременные данные платформы:

users;
chats;
messages;
document metadata;
chunks;
embeddings;
другие persistent данные.

Технологии:
PostgreSQL + pgvector.

Зачем разделять:
ты являешься постоянным источником истины
и имеет отдельные требования к backup,
recovery, consistency и масштабированию.

Основные зависимости:
Не зависит от прикладных контейнеров.
Используется Core API, AI Orchestrator и Document Ingestion Worker.

### Object Storage

Ответственность:
Хранит бинарные файлы:

оригинальные документы;
сгенерированные файлы;
другие крупные объекты.

Технологии:
S3-compatible Object Storage.
Для локального проекта - MinIO.

Зачем разделять:
Большие бинарные файлы нецелесообразно смешивать
с основной реляционной базой данных.

Основные зависимости:
Используется Core API и Document Ingestion Worker.

### Redis

Ответственность:
Хранит временные данные и cache,
к которым нужен быстрый доступ.

Технологии:
Redis.

Зачем разделять:
Позволяет получать временные и часто используемые данные
быстрее основной базы.

Redis не является source of truth
для критичных данных платформы.

Основные зависимости:
Используется Core API и AI Orchestrator.

### Local LLM Inference

Ответственность:
Выполняет inference локально размещённых LLM.

Технологии:
vLLM.

В дальнейшем:
GPU runtime и локальные модели, например Qwen или GPT-OSS.

Зачем разделять:
Позволяет выполнять AI inference внутри инфраструктуры организации
и снижает необходимость передачи корпоративных данных
внешним LLM провайдерам.

Основные зависимости:

- model weights;
- GPU/compute infrastructure.
