using System.Security.Cryptography;

namespace RideMate.Application.Services;

public class InviteService
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string GenerateInviteCode(int length = 6)
    {
        return string.Create(length, Alphabet, (span, alpha) => {
            for (int i = 0; i < span.Length; i++)
            {
                // Correct: Assign a single char using a random index
                int randomIndex = RandomNumberGenerator.GetInt32(alpha.Length);
                span[i] = alpha[randomIndex];
            }
        });
    }
}