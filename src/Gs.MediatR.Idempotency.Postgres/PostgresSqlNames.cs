using System.Text.RegularExpressions;

namespace Gs.MediatR.Idempotency.Postgres;

/// <summary>
/// Identificadores SQL já validados e entre aspas. Nomes de schema/tabela vêm de configuração e são
/// concatenados no SQL (identificadores não podem ser parâmetros), então são restritos a [A-Za-z0-9_].
/// </summary>
internal sealed class PostgresSqlNames
{
    private static readonly Regex Identifier = new("^[A-Za-z_][A-Za-z0-9_]{0,40}$", RegexOptions.Compiled);

    public PostgresSqlNames(PostgresIdempotencyOptions options)
    {
        Validate(options.Schema, nameof(options.Schema));
        Validate(options.TableName, nameof(options.TableName));

        Schema = Quote(options.Schema);
        Table = $"{Schema}.{Quote(options.TableName)}";
        PrimaryKey = Quote($"pk_{options.TableName}");
        ExpiresIndex = Quote($"ix_{options.TableName}_expires_at");
    }

    /// <summary>"schema"</summary>
    public string Schema { get; }

    /// <summary>"schema"."table"</summary>
    public string Table { get; }

    public string PrimaryKey { get; }

    public string ExpiresIndex { get; }

    private static string Quote(string identifier) => "\"" + identifier + "\"";

    private static void Validate(string value, string name)
    {
        if (string.IsNullOrEmpty(value) || !Identifier.IsMatch(value))
        {
            throw new ArgumentException(
                $"Invalid PostgreSQL identifier for {name}: '{value}'. Use letters, digits and '_' only (max 41 chars).");
        }
    }
}
