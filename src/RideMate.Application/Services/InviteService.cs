// using System.Security.Cryptography;


// namespace RideMate.Application.Services;

// public class InviteService
// {
//     private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No ambiguous chars

//     public string GenerateInviteCode(int length = 6)
//     {
//         return string.Create(length, length, (span, state) => {
//             for (int i = 0; i < span.Length; i++)
//                 span[i] = Alphabet;
//         });
//     }
// }
// using System.Security.Cryptography;

// namespace RideMate.Application.Services;

// public class InviteService
// {
//     private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No ambiguous chars

//     public string GenerateInviteCode(int length = 6)
//     {
//         // We pass 'Alphabet' as the state to avoid memory allocations from a closure
//         return string.Create(length, Alphabet, (span, alpha) => {
//             for (int i = 0; i < span.Length; i++)
//             {
//                 // Select one character from the string using a cryptographically secure random index
//                 int randomIndex = RandomNumberGenerator.GetInt32(alpha.Length);
//                 span[i] = alpha[randomIndex];
//             }
//         });
//     }
// }
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