namespace Akinai.Shared.Dtos.Account;

public record TwoFactorStatusDto(
    bool Is2faEnabled,
    bool IsMachineRemembered,
    int RecoveryCodesLeft,
    bool HasAuthenticator);
