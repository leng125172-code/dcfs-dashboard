using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;

namespace WhaleDeck.Infrastructure.Secrets;

public sealed class MemoryOneTimeSecretStore : IOneTimeSecretStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, SecretEntry> _entries = new(StringComparer.Ordinal);

    public Task<OneTimeSecretTicketDto> StoreAsync(string ownerSubject, string secret, CancellationToken cancellationToken)
    {
        Validate(ownerSubject, secret);
        cancellationToken.ThrowIfCancellationRequested();
        var expiresAt = DateTimeOffset.UtcNow.Add(Lifetime);
        while (true)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            if (_entries.TryAdd(Key(ownerSubject, token), new SecretEntry(secret, expiresAt)))
            {
                return Task.FromResult(new OneTimeSecretTicketDto(token, expiresAt));
            }
        }
    }

    public Task<string?> ConsumeAsync(string ownerSubject, string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsTokenValid(token) || !_entries.TryRemove(Key(ownerSubject, token), out var entry) || entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return Task.FromResult<string?>(null);
        }
        return Task.FromResult<string?>(entry.Value);
    }

    internal static string Key(string ownerSubject, string token)
    {
        var ownerHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ownerSubject)));
        return $"whaledeck:secret:{ownerHash}:{token}";
    }

    internal static bool IsTokenValid(string token) => token.Length == 64 && token.All(Uri.IsHexDigit);

    internal static void Validate(string ownerSubject, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        if (secret.Length > 65_536) throw new ArgumentOutOfRangeException(nameof(secret), "One-time secret is too large.");
    }

    private sealed record SecretEntry(string Value, DateTimeOffset ExpiresAtUtc);
}
