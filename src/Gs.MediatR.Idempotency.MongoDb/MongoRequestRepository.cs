using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Gs.MediatR.Idempotency.MongoDb;

internal sealed class MongoRequestRepository : IRequestRepository
{
    private static readonly FilterDefinitionBuilder<RequestDocument> F = Builders<RequestDocument>.Filter;
    private static readonly UpdateDefinitionBuilder<RequestDocument> U = Builders<RequestDocument>.Update;

    private readonly IMongoCollection<RequestDocument> _collection;
    private readonly TimeProvider _time;

    public MongoRequestRepository(IMongoClient client, IOptions<MongoIdempotencyOptions> options, TimeProvider time)
    {
        var o = options.Value;
        _collection = client.GetDatabase(o.DatabaseName).GetCollection<RequestDocument>(o.CollectionName);
        _time = time;
    }

    public async Task<bool> IsCompletedAsync(RequestKey key, CancellationToken cancellationToken = default)
    {
        var filter = F.Eq(d => d.Id, DocumentId(key))
                   & F.Eq(d => d.Status, RequestStatus.Completed)
                   & F.Gt(d => d.ExpiresAtUtc, Now()); // expirado (mas ainda não apagado pelo TTL) = inexistente

        return await _collection.Find(filter).Limit(1).AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ReservationResult> TryReserveAsync(
        RequestKey key, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var id = DocumentId(key);

        // Algumas tentativas cobrem a corrida em que o documento some (TTL/Release) entre os passos.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var now = Now();

            // 1) Tenta INSERIR. O _id único torna isto atômico: só um concorrente consegue.
            try
            {
                await _collection.InsertOneAsync(
                    new RequestDocument
                    {
                        Id = id,
                        Scope = key.Scope,
                        RequestId = key.Id,
                        Status = RequestStatus.Processing,
                        CreatedAtUtc = now,
                        ExpiresAtUtc = now.Add(leaseDuration)
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                return ReservationResult.Acquired;
            }
            catch (Exception ex) when (IsDuplicateKey(ex))
            {
                // Já existe: segue para o passo 2.
            }

            // 2) Existe, mas EXPIRADO (lease abandonado ou retenção vencida)? Assume atomicamente.
            //    O filtro inclui ExpiresAtUtc <= now, então só um concorrente "ganha" (ModifiedCount == 1).
            var takeover = await _collection.UpdateOneAsync(
                F.Eq(d => d.Id, id) & F.Lte(d => d.ExpiresAtUtc, now),
                U.Set(d => d.Status, RequestStatus.Processing)
                 .Set(d => d.CreatedAtUtc, now)
                 .Set(d => d.ExpiresAtUtc, now.Add(leaseDuration)),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (takeover.ModifiedCount == 1)
            {
                return ReservationResult.Acquired;
            }

            // 3) Existe e não expirou: descobre em que estado está.
            var existing = await _collection.Find(F.Eq(d => d.Id, id))
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                continue; // sumiu no meio do caminho: tenta de novo
            }

            return existing.Status == RequestStatus.Completed
                ? ReservationResult.AlreadyCompleted
                : ReservationResult.InProgress;
        }

        return ReservationResult.InProgress;
    }

    public async Task MarkCompletedAsync(RequestKey key, TimeSpan retention, CancellationToken cancellationToken = default)
    {
        var id = DocumentId(key);

        // Upsert: funciona tanto após uma reserva (Eager) quanto sem ela (Lazy).
        // Dois upserts simultâneos podem colidir na criação (duplicate key): a segunda tentativa vira update.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var now = Now();
            try
            {
                await _collection.UpdateOneAsync(
                    F.Eq(d => d.Id, id),
                    U.Set(d => d.Status, RequestStatus.Completed)
                     .Set(d => d.ExpiresAtUtc, now.Add(retention))
                     .SetOnInsert(d => d.Scope, key.Scope)
                     .SetOnInsert(d => d.RequestId, key.Id)
                     .SetOnInsert(d => d.CreatedAtUtc, now),
                    new UpdateOptions { IsUpsert = true },
                    cancellationToken).ConfigureAwait(false);

                return;
            }
            catch (Exception ex) when (attempt == 0 && IsDuplicateKey(ex))
            {
                // tenta novamente
            }
        }
    }

    public async Task ReleaseAsync(RequestKey key, CancellationToken cancellationToken = default)
    {
        // Só remove reservas em andamento; nunca apaga um registro já concluído.
        await _collection.DeleteOneAsync(
            F.Eq(d => d.Id, DocumentId(key)) & F.Eq(d => d.Status, RequestStatus.Processing),
            cancellationToken).ConfigureAwait(false);
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;

    private static string DocumentId(RequestKey key) => $"{key.Scope}|{key.Id}";

    private static bool IsDuplicateKey(Exception ex) => ex switch
    {
        MongoWriteException w => w.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoBulkWriteException b => b.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey),
        _ => false
    };
}
