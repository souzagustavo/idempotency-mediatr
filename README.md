# Gs.MediatR.Idempotency

Idempotência para consumidores de mensageria usando **MediatR Pipeline Behavior** (padrão *Idempotent Consumer*).
O consumidor do broker despacha um command MediatR; o `IdempotencyBehavior` impede que a mesma mensagem execute o handler duas vezes.

```
Broker ──> Consumer ──Send(command)──> [IdempotencyBehavior] ──> Handler
                                              │
                                              └── IRequestRepository (MongoDB | PostgreSQL)
```

## Estrutura

```
src/
  Gs.MediatR.Idempotency.Abstraction   IIdempotentRequest, IRequestRepository, IdempotencyBehavior, opções, DI
  Gs.MediatR.Idempotency.MongoDb       MongoRequestRepository + índice TTL         → .UseMongoDb()
  Gs.MediatR.Idempotency.Postgres      PostgresRequestRepository + DDL + cleanup   → .UsePostgres()
samples/
  Gs.MediatR.Idempotency.AppHost            Aspire: sobe MongoDB e PostgreSQL (containers) e os dois samples
  Gs.MediatR.Idempotency.ServiceDefaults    OpenTelemetry + health checks
  Gs.MediatR.Idempotency.Samples.Common     command, handler, "broker" em memória, consumidor, endpoints
  Gs.MediatR.Idempotency.Samples.MongoDb    Program.cs com Mongo   (modo Lazy)
  Gs.MediatR.Idempotency.Samples.Postgres   Program.cs com Postgres (modo Eager)
```

## Como usar

```csharp
// 1) O command implementa IIdempotentRequest
public sealed record ProcessPaymentCommand(string MessageId, Guid OrderId, decimal Amount)
    : IRequest, IIdempotentRequest
{
    string IIdempotentRequest.IdempotencyKey => MessageId;   // id gerado pelo PRODUTOR
}

// 2) Registro
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddIdempotencyBehavior();                // registre DEPOIS do behavior de transação, se houver
});

services.AddIdempotency(o => o.Mode = IdempotencyMode.Lazy)
        .UseMongoDb();                           // ou .UsePostgres()
```

## Modos

| | **Lazy** (padrão do artigo) | **Eager** |
|---|---|---|
| Fluxo | já concluído? → handler → grava | reserva atômica → handler → grava (falhou: libera) |
| Falha do handler | nada gravado; reentrega reprocessa | reserva liberada; reentrega reprocessa |
| Entregas simultâneas | **ambas podem executar o handler** | a 2ª recebe `RequestInProgressException` (NACK → reentrega depois) |
| Chamadas ao banco | 2 | 2 (3 se houver falha) |

Registros expiram: concluídos após `Retention` (padrão 7 dias — deve exceder a janela de reentrega do broker);
reservas após `ProcessingLease` (padrão 5 min — recupera reservas de processos que caíram).
MongoDB usa índice TTL; PostgreSQL usa um `BackgroundService` de limpeza.

## Executar os samples

Requisitos: **.NET 10 SDK** e **Docker** (ou Podman).

```bash
dotnet run --project samples/Gs.MediatR.Idempotency.AppHost
```

O dashboard do Aspire abre em `http://localhost:15180`; lá estão os endpoints de `sample-mongodb` (Lazy) e `sample-postgres` (Eager).

```bash
curl -X POST "<url>/payments"                              # 1 mensagem entregue 2x, com 500 ms de intervalo
curl -X POST "<url>/payments?duplicates=5&delayMs=0"       # 5 entregas simultâneas
curl -X POST "<url>/payments?simulateFailure=true"         # 1ª tentativa do handler falha
curl "<url>/stats"
```

Resultado esperado em `/stats` (cada `sideEffectsPerOrder` deve ser 1):

| Cenário | Lazy | Eager |
|---|---|---|
| `duplicates=2` (500 ms) | 1 efeito | 1 efeito |
| `simulateFailure=true` | 1 efeito (retry após falha) | 1 efeito (retry após falha) |
| `duplicates=5&delayMs=0` | **pode dar >1** (corrida: limitação do Lazy) | 1 efeito (protegido) |

## Limitações conhecidas

1. **Atomicidade com o efeito colateral.** O repositório grava o estado em conexão própria. Se o handler concluir e o processo cair antes de
   `MarkCompletedAsync`, a mensagem será reprocessada (at-least-once). Para efeitos externos, repasse o `IdempotencyKey` como chave de
   idempotência ao destino. Participar da mesma transação do handler (Unit of Work) seria uma evolução natural.
2. **Duplicata retorna `default`.** Para commands sem retorno (`IRequest`) é natural; se o seu retorna dados, o chamador precisa tolerar `default`.
3. **Escopo = tipo do request.** Um request tem um handler no MediatR, então equivale a "um consumidor".

## Versões

`Directory.Packages.props` centraliza as versões. **MediatR está em 12.5.0** (a última sem license key; v13+ é comercial).
O SDK do AppHost (`Aspire.AppHost.Sdk/13.0.0` no `.csproj`) precisa ter a mesma versão dos pacotes `Aspire.*` (`AspireVersion`);
para atualizar tudo use `aspire update` ou altere os dois pontos.
