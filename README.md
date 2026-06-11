# Trail — Backend API

API REST do **Projeto Trail**, plataforma de gestão de trilhas de aprendizagem, desafios técnicos e fluxos de mentoria para o Programa Residência Porto Digital, com mentoria técnica da Avanade.

---

## Stack

| Camada | Tecnologia |
|--------|-----------|
| Runtime | ASP.NET Core (.NET 10) |
| ORM | Entity Framework Core |
| Banco | SQL Server |
| Auth | JWT Bearer |
| Docs | Swagger / OpenAPI (Swashbuckle) |
| Cloud | Azure App Service + Azure SQL |

---

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server local **ou** Docker
- [`dotnet-ef`](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) CLI tool

```bash
dotnet tool install --global dotnet-ef
```

---

## Como rodar localmente

### 1. Clonar o repositório

```bash
git clone <url-do-repo>
cd trail-backend
```

### 2. Subir o SQL Server

O projeto inclui um arquivo `docker-compose.yml` para facilitar a configuração do banco.

**Via Docker Compose (Recomendado):**
```bash
docker compose up -d
```

**Via Docker Run (Alternativa):**
```bash
docker run \
  -e "ACCEPT_EULA=Y" \
  -e "SA_PASSWORD=YourStrong@Password123" \
  -p 1433:1433 \
  --user 0 \
  -d mcr.microsoft.com/mssql/server:2019-latest
```

### 3. Configurar o ambiente de desenvolvimento

O arquivo `Trail.Api/appsettings.Development.json` **não é versionado** (está no `.gitignore`).  
Crie-o na raiz do projeto `Trail.Api/` (ou use o existente):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=TrailDb;User Id=sa;Password=YourStrong@Password123;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Secret": "trail-super-secret-key-for-dev-only-32chars!!"
  }
}
```

> Em produção, use variáveis de ambiente ou Azure Key Vault — nunca versione segredos.

### 4. Aplicar as migrations

```bash
dotnet ef database update --project Trail.Api
```

### 5. Rodar a API

```bash
dotnet run --project Trail.Api
```

A API estará disponível em:
- `https://localhost:7xxx` — HTTPS
- `http://localhost:5xxx` — HTTP

Swagger UI: `https://localhost:7xxx/swagger`  
Spec OpenAPI: `https://localhost:7xxx/openapi/v1.json`

---

## Testes

Os testes ficam no projeto `Trail.Api.Tests/` (xUnit) e **não dependem de banco de dados**: os testes unitários usam o provedor **EF Core InMemory** e os de integração sobem a API com `WebApplicationFactory`, trocando o SQL Server por um banco em memória. Ou seja, basta o .NET SDK — não é preciso subir o Docker nem aplicar migrations.

### Rodar todos os testes

```bash
dotnet test
```

> Há dois arquivos de solução na raiz (`Trail.slnx` e `trail-backend.sln`). Caso o comando acima fique ambíguo, aponte a solução ou o projeto explicitamente:
>
> ```bash
> dotnet test Trail.slnx
> # ou
> dotnet test Trail.Api.Tests/Trail.Api.Tests.csproj
> ```

### Rodar um teste específico (filtro)

```bash
dotnet test --filter "FullyQualifiedName~AuthServiceTests"
```

### O que é coberto

| Camada | Arquivo | Foco |
|--------|---------|------|
| Unitário | `Services/AuthServiceTests.cs` | Registro (email duplicado), login (senha certa/errada), hash de senha |
| Unitário | `Services/TokenServiceTests.cs` | Claims do JWT (`sub`/`email`/`role`) e expiração |
| Unitário | `Services/TrailServiceTests.cs` | Filtro por nível, busca textual, desafios ordenados |
| Unitário | `Services/SubmissionServiceTests.cs` | Regras de submissão e revisão, fila de pendentes |
| Integração | `Integration/EndpointsIntegrationTests.cs` | `/health`, 401 sem token, login, RBAC (403) |

---

## Autenticação — exemplos rápidos

Fluxo básico: `login` retorna `token` (JWT) e `refreshToken` — o cliente usa o JWT no header `Authorization: Bearer <token>` e usa o `refreshToken` para renovar quando o JWT expirar.

Exemplo `POST /auth/login` (body):

```json
{
  "email": "student@example.com",
  "password": "Password1!"
}
```

Resposta (200):

```json
{
  "token": "<jwt>",
  "refreshToken": "<refresh-token>",
  "role": "Student",
  "name": "Student Name"
}
```

Exemplo `POST /auth/refresh` (body):

```json
{
  "refreshToken": "<refresh-token>"
}
```

Resposta (200):

```json
{
  "token": "<new-jwt>",
  "refreshToken": "<new-refresh-token>",
  "role": "Student",
  "name": "Student Name"
}
```

Exemplo `POST /auth/logout` (body):

```json
{
  "refreshToken": "<refresh-token-to-revoke>"
}
```

Resposta: `204 No Content` (quando válido)

Configuração do tempo de expiração do refresh token (dev): no `appsettings.Development.json` adicione a seção:

```json
"Refresh": {
  "ExpirationDays": 14
}
```


## Endpoints

### Implementados

| Método | Rota | Role | Descrição |
|--------|------|------|-----------|
| `GET` | `/health` | Público | Liveness check da API |
| `GET` | `/health/detailed` | Mentor / Manager | Readiness check (verifica conexão com o banco) |
| `POST` | `/auth/register` | Público | Cria conta e retorna JWT |
| `POST` | `/auth/login` | Público | Autentica e retorna JWT |
| `GET` | `/auth/me` | Autenticado | Retorna os dados do usuário autenticado |

### Planejados (MVP)

| Método | Rota | Role | Descrição |
|--------|------|------|-----------|
| `GET` | `/trails` | Autenticado | Lista trilhas |
| `GET` | `/trails/{id}/challenges` | Autenticado | Desafios de uma trilha |
| `POST` | `/submissions` | Student | Submete uma entrega |
| `GET` | `/submissions` | Mentor | Lista entregas pendentes |
| `PUT` | `/submissions/{id}/review` | Mentor | Avalia uma entrega |
| `GET` | `/students/{id}/progress` | Autenticado | Progresso do estudante |
| `GET` | `/metrics/overview` | Mentor / Manager | KPIs da turma |

---

## KPIs (calculados dinamicamente, nunca persistidos)

| KPI | Fórmula |
|-----|---------|
| Lead Time de Feedback | `ReviewedAt − SubmittedAt` |
| Taxa de Conclusão | `desafios concluídos ÷ total (%)` |
| Cobertura de Desafios | `submissões avaliadas ÷ submissões entregues (%)` |

---

## Modelo de dados

```
User        — Id, Name, Email, PasswordHash, Role, CreatedAt
Trail       — Id, Name, Description, CreatedAt
Challenge   — Id, TrailId (FK), Title, Description, Order, CreatedAt
Submission  — Id, StudentId (FK), ChallengeId (FK), DeliveryUrl,
              SubmittedAt, Status, ReviewerId (FK), Score, Feedback, ReviewedAt
```

> A avaliação está embutida na `Submission` — não existe entidade `Review` no MVP (decisão intencional).

---

## Estrutura de pastas

```
trail-backend/
│
├── Trail.Api/                         # Projeto principal da API
│   │
│   ├── Configuration/                 # Classes de opções tipadas
│   │   └── JwtOptions.cs              # Valida Jwt:Secret/Issuer/Audience no startup
│   │
│   ├── Controllers/                   # Endpoints HTTP (controllers finos, sem lógica)
│   │   ├── AuthController.cs          # /auth/register, /auth/login, /auth/me
│   │   └── HealthController.cs        # /health, /health/detailed
│   │
│   ├── Domain/                        # Núcleo do domínio (sem dependências externas)
│   │   ├── Entities/                  # Entidades persistidas no banco
│   │   │   ├── User.cs
│   │   │   ├── Trail.cs
│   │   │   ├── Challenge.cs
│   │   │   └── Submission.cs
│   │   └── Enums/                     # Tipos enumerados do domínio
│   │       ├── UserRole.cs            # Student | Mentor | Manager
│   │       └── SubmissionStatus.cs    # Submitted | Reviewed
│   │
│   ├── Application/                   # Lógica de negócio e orquestração
│   │   └── Services/
│   │       ├── AuthService.cs         # Fluxo de registro e login
│   │       ├── ITokenService.cs       # Contrato para geração de JWT
│   │       └── TokenService.cs        # Implementação — lê JwtOptions via IOptions<T>
│   │
│   ├── Extensions/                    # Extension methods para organizar o startup
│   │   └── ServiceCollectionExtensions.cs  # AddDatabase, AddJwtAuthentication, AddApplicationServices
│   │
│   ├── Infrastructure/                # Detalhes de infraestrutura
│   │   └── Data/
│   │       ├── AppDbContext.cs        # DbContext com mapeamentos EF Core
│   │       └── DbSeeder.cs            # Seed inicial de usuários (async, com logging)
│   │
│   ├── DTOs/                          # Objetos de transferência de dados
│   │   └── Auth/
│   │       ├── LoginRequest.cs        # { Email*, Password* } — validação via DataAnnotations
│   │       ├── RegisterRequest.cs     # { Name*, Email*, Password*, Role* }
│   │       └── LoginResponse.cs       # { Token, Role, Name }
│   │
│   ├── Migrations/                    # Migrations geradas pelo EF Core
│   ├── Program.cs                     # Composição da aplicação (usa extension methods)
│   ├── appsettings.json               # Configurações base (sem segredos)
│   └── appsettings.Development.json   # Configurações locais (NÃO versionado)
│
├── .github/                           # Configurações GitHub
│   ├── workflows/
│   │   └── ci.yml                     # Pipeline CI: build + testes
│   ├── PULL_REQUEST_TEMPLATE/         # Template de PR
│   ├── ISSUE_TEMPLATE/                # Templates de bug e feature
│   ├── CODEOWNERS                     # Responsáveis por revisão
│   └── copilot-instructions.md        # Instruções para o GitHub Copilot
│
├── project-infos/                     # Documentação da mentoria (sem código)
│   ├── discovery/                     # Briefing e discovery do produto
│   ├── encontros/                     # Material de cada encontro
│   ├── atividades/                    # Desafios técnicos e checklists
│   ├── entregas/                      # Artefatos produzidos pelos alunos
│   └── referencias/                   # Boas práticas, links, arquitetura
│
├── CLAUDE.md                          # Instruções para o Claude Code
├── Trail.slnx                         # Arquivo de solução .NET
└── .gitignore                         # Arquivos ignorados pelo git
```

---

## Contrato de erros

Todos os erros seguem **RFC 7807 (ProblemDetails)**:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Conflict",
  "status": 409,
  "detail": "Email já cadastrado."
}
```

| Status | Situação |
|--------|----------|
| `400` | Dados de entrada inválidos (validação automática via `[ApiController]`) |
| `401` | Credenciais incorretas ou token ausente |
| `403` | Token válido, mas role insuficiente |
| `409` | Conflito de recurso (ex: email duplicado) |
| `503` | Dependência indisponível (ex: banco fora do ar) |
| `500` | Erro inesperado no servidor |

---

## Regras de arquitetura

- **Controllers** são finos — sem lógica de negócio
- **Lógica de negócio** fica nos `Services` (Application layer)
- **Geração de JWT** é responsabilidade de `ITokenService` / `TokenService`, não de `AuthService`
- **KPIs** são sempre calculados dinamicamente, nunca persistidos no banco
- **Autorização** por Role é feita no backend (`[Authorize(Roles = "Mentor")]`)
- **Frontend** não implementa regras de negócio
- **Configuração obrigatória** (`Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`, `DefaultConnection`) é validada no startup — a aplicação não sobe com valores ausentes

---

## Fora do escopo do MVP

Não implementar: chat, gamificação, white-label, relatórios interprogramas, upload de arquivo (apenas `DeliveryUrl` como link).

---

## Convenções

- Rotas em inglês, snake_case
- Erros retornam `ProblemDetails` (RFC 7807)
- Toda migration deve ser revisada antes do commit
- Nunca commitar `appsettings.Development.json`
- Swagger/OpenAPI sempre atualizado

---

**Mentoria técnica: Avanade | Programa: Porto Digital — Residência**
