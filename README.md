# Gs.MediatR.Idempotency

Implementando o padrão de idempotência para `Requests` usando **MediatR Pipeline Behavior**.

## Caso de Uso

Um consumidor de broker de mensagens despacha um request usando MediatR, o `IdempotencyBehavior` é a camada que garante que uma mesma solicitação do mesmo request execute o handler duas vezes ou mais

```
Broker ──> Consumer/Subscriber ──Send(command)──> [IdempotencyBehavior] ──> Handler
                                              │
                                              └── IRequestRepository (MongoDB | ...)
```

O uso é recomendado para **requets** (sem retorno) tipicos de processamentos inicializados pelo consumo de mensagens.

## Estrutura

```
src/
  Gs.MediatR.Idempotency.Abstraction   Abstrações para implementação de idempotência para demais estruturas de persistência
  Gs.MediatR.Idempotency.MongoDb       Implementação usando MongoDb → .UseMongoDb()  
samples/
  Gs.MediatR.Idempotency.AppHost            
  Gs.MediatR.Idempotency.ServiceDefaults    
  Gs.MediatR.Idempotency.Samples.Common     Request, handler, "broker" em memória, consumidor, endpoints
  Gs.MediatR.Idempotency.Samples.MongoDb    Exemplo  de uso com MongoDB 
```

## Como usar

### Configurando Requests

O request precisa implementar a interface IIdempotentRequest, definindo a propriedade IdempotencyKey como chave única para identificar a mensagem.

No exemplo abaixo, a chave é o MessageId. 

```csharp
// 
public sealed record ProcessPaymentRequest(string MessageId, Guid OrderId, decimal Amount)
    : IRequest, IIdempotentRequest
{
    string IIdempotentRequest.IdempotencyKey => MessageId; 
}
```

### Registro do Behavior

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddIdempotencyBehavior(); 
});
```

### Configurando Estratégia de Persistência

#### Usando MongoDB

Registre o uso do MongoDB como repositório de idempotência. O `IMongoClient` precisa estar registrado no container.

```csharp

builder.Services
    .AddMediatRIdempotency(options => builder.Configuration.GetSection("IdempotencyMediatR").Bind(options))
    .UseMongoDb(options => options.DatabaseName = "your-database");

```

### Lock Distribuído

O Pipeline Behavior precisa de um lock distribuído para impedir que duas ou mais instâncias do mesmo request executem o handler ao mesmo tempo.

Sendo assim é necessário registrar um `IDistributedLockProvider`

#### Usando via MongoDB

Caso você esteja usando MongoDB e não possui nenhum um provedor de Lock configurado, instale o pacote `DistributedLock.MongoDB` e registre o provider.

```
dotnet add package DistributedLock.MongoDB
```

```csharp
builder.Services.AddScoped<IDistributedLockProvider>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new MongoDistributedSynchronizationProvider(db);
});
```

#### Demais Provedores

É possivel usar qualquer provedor de lock que seja compativel com `IDistributedLockProvider`.

Consulte as opções em [madelson/DistributedLock: A .NET library for distributed synchronization](https://github.com/madelson/DistributedLock)

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
