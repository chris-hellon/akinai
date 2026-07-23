namespace Akinai.Shared.Dtos.Account;

public record ProfileDto(
    string Username,
    string Email,
    string? PhoneNumber,
    bool HasPassword,
    bool TwoFactorEnabled,
    int PasskeyCount
);
