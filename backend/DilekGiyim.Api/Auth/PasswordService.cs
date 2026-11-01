using System.Security.Cryptography;
using System.Text;
using DilekGiyim.Api.Data;
using DilekGiyim.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DilekGiyim.Api.Auth;

/// <summary>
/// Şifre özeti (ASP.NET Core Identity PasswordHasher: PBKDF2, tuzlu), kilitleme ve sıfırlama token'ları.
/// </summary>
public class PasswordService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);

    private readonly PasswordHasher<object> _hasher = new();
    private readonly string _dummyHash;
    private static readonly object User = new();

    public PasswordService()
    {
        // Kullanıcı bulunamadığında da aynı sürede yanıt vermek için
        _dummyHash = _hasher.HashPassword(User, Guid.NewGuid().ToString());
    }

    public string Hash(string password) => _hasher.HashPassword(User, password);

    /// <summary>Doğruysa true; hash eskiyse yenisi rehash içinde döner.</summary>
    public bool Verify(string? hash, string password, out string? rehash)
    {
        rehash = null;
        var result = _hasher.VerifyHashedPassword(User, hash ?? _dummyHash, password);
        if (hash is null) return false;
        if (result == PasswordVerificationResult.SuccessRehashNeeded) rehash = Hash(password);
        return result != PasswordVerificationResult.Failed;
    }

    /// <summary>En az 8 karakter, en az bir harf ve bir rakam.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return "Şifre en az 8 karakter olmalı.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "Şifre en az bir harf ve bir rakam içermeli.";
        if (password.Length > 128) return "Şifre çok uzun.";
        return null;
    }

    /// <summary>Tek kullanımlık sıfırlama token'ı üretir; veritabanına yalnızca özeti yazılır.</summary>
    public async Task<string> CreateResetTokenAsync(AppDbContext db, AccountType type, int userId)
    {
        // Aynı kullanıcının önceki kullanılmamış token'larını geçersiz kıl
        var old = await db.PasswordResetTokens
            .Where(t => t.AccountType == type && t.UserId == userId && t.UsedAt == null)
            .ToListAsync();
        old.ForEach(t => t.UsedAt = DateTime.UtcNow);

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            AccountType = type,
            UserId = userId,
            TokenHash = HashToken(token),
            ExpiresAt = DateTime.UtcNow.Add(ResetTokenLifetime)
        });
        await db.SaveChangesAsync();
        return token;
    }

    /// <summary>Geçerliyse token'ı kullanılmış işaretler ve kullanıcı kimliğini döner.</summary>
    public async Task<int?> ConsumeResetTokenAsync(AppDbContext db, AccountType type, string token)
    {
        var hash = HashToken(token ?? "");
        var row = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.AccountType == type);
        if (row is null || row.UsedAt is not null || row.ExpiresAt < DateTime.UtcNow) return null;
        row.UsedAt = DateTime.UtcNow;
        return row.UserId;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
