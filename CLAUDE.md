# Trail — Backend API (ASP.NET Core)

## O que é este projeto

API REST do **Projeto Trail** — plataforma de gestão de trilhas de aprendizagem para o Programa Residência Porto Digital, com mentoria técnica da Avanade.

Documentação completa em `project-infos/discovery/discovery.md` (fonte única da verdade).

---

## Stack

- **Runtime:** ASP.NET Core (C#) + .NET 8+
- **ORM:** Entity Framework Core + SQL Server
- **Auth:** JWT + ASP.NET Identity
- **Docs:** Swagger / OpenAPI
- **Cloud:** Azure App Service + Azure SQL

---

## Entidades do domínio

```
User        — Id(GUID), Name, Email, PasswordHash, Role(Student/Mentor/Manager), CreatedAt
Trail       — Id, Name, Description, CreatedAt
Challenge   — Id, TrailId(FK), Title, Description, Order, CreatedAt
Submission  — Id, StudentId(FK), ChallengeId(FK), DeliveryUrl, SubmittedAt,
              Status(Submitted/Reviewed), ReviewerId(FK), Score, Feedback, ReviewedAt
```

**Decisão de design:** A avaliação está embutida na `Submission` — não existe entidade `Review` no MVP.

---

## Endpoints do MVP

| Método | Rota | Role | MVP |
|--------|------|------|-----|
| POST | `/auth/login` | Público | 01 |
| GET | `/trails` | Autenticado | 02 |
| GET | `/trails/{id}/challenges` | Autenticado | 02 |
| POST | `/submissions` | Student | 03 |
| GET | `/submissions` | Mentor | 04 |
| PUT | `/submissions/{id}/review` | Mentor | 04 |
| GET | `/students/{id}/progress` | Autenticado | 05 |
| GET | `/metrics/overview` | Mentor/Manager | 05 |

---

## KPIs (calculados dinamicamente, nunca persistidos)

- **Lead Time** = `ReviewedAt - SubmittedAt`
- **Taxa de Conclusão** = desafios concluídos ÷ total (%)
- **Cobertura** = submissões avaliadas ÷ submissões entregues (%)

---

## Regras de arquitetura

- Controllers são finos — sem lógica de negócio
- Lógica de negócio fica nos Services (Application layer)
- KPIs são sempre derivados, nunca persistidos no banco
- Multi-tenancy via coluna — Global Query Filters no EF Core
- Autorização por Role no backend (`[Authorize(Roles = "Mentor")]`)
- Frontend não implementa regras de negócio (não é responsabilidade desta API)

---

## Estrutura de pastas esperada

```
Trail.Api/
├── Controllers/
├── Domain/
│   ├── Entities/
│   └── Enums/
├── Application/
│   └── Services/
├── Infrastructure/
│   ├── Data/         (DbContext, migrations)
│   └── Repositories/
└── DTOs/
```

---

## O que está fora do MVP

Não implementar: chat, gamificação, white-label, relatórios interprogramas, upload de arquivo (apenas DeliveryUrl por link).

---

## Convenções de código

- Nomear endpoints em inglês, snake_case para rotas
- Retornar `ProblemDetails` em erros (RFC 7807)
- Toda migration deve ser revisada antes do commit
- Nunca commitar `appsettings.json` com connection strings reais — usar variáveis de ambiente ou `appsettings.Development.json` (no .gitignore)
- Swagger deve estar habilitado e atualizado

---

## Como rodar localmente

```bash
# 1. Subir SQL Server (Docker Compose)
docker compose up -d

# 2. Restaurar dependências
dotnet restore

# 3. Aplicar migrations
dotnet ef database update --project Trail.Api

# 4. Rodar a API
dotnet run --project Trail.Api
```

A API estará disponível em `http://localhost:5108` com Swagger em `/swagger`.
