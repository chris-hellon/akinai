using System.Buffers.Text;
using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Account;
using InvoiceSoftware.Shared.Dtos.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace InvoiceSoftware.Infrastructure.Handlers.Account;

/// <summary>
/// Passkey management for the signed-in account.
///
/// Registration is deliberately split in two: the browser cannot mint a
/// credential without a server-issued challenge, and the server will not store
/// one it did not challenge for. <see cref="GetPasskeyCreationOptionsHandler"/>
/// issues that challenge (Identity keeps the state server-side, keyed to the
/// session) and <see cref="AddPasskeyHandler"/> verifies the result against it.
///
/// Passkey *sign-in* is not here and cannot be: it has to run before there is a
/// session, on the statically-rendered login page that establishes the auth
/// cookie.
/// </summary>
public class GetPasskeysHandler(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor httpContextAccessor) : IHandle<GetPasskeys, List<PasskeyDto>>
{
    public async Task<HttpResult<List<PasskeyDto>>> Handle(GetPasskeys request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext?.User!);
        if (user is null) return HttpResult<List<PasskeyDto>>.Unauthorized();

        var passkeys = await userManager.GetPasskeysAsync(user);

        // Newest first: the one you just added is the one you are looking for.
        return HttpResult<List<PasskeyDto>>.Ok(passkeys
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PasskeyDto(Base64Url.EncodeToString(p.CredentialId), p.Name, p.CreatedAt))
            .ToList());
    }
}

public class GetPasskeyCreationOptionsHandler(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IHttpContextAccessor httpContextAccessor) : IHandle<GetPasskeyCreationOptions, string>
{
    public async Task<HttpResult<string>> Handle(GetPasskeyCreationOptions request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext?.User!);
        if (user is null) return HttpResult<string>.Unauthorized();

        var userId = await userManager.GetUserIdAsync(user);
        var userName = await userManager.GetUserNameAsync(user) ?? "User";

        var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new()
        {
            Id = userId,
            Name = userName,
            DisplayName = userName
        });

        return HttpResult<string>.Ok(optionsJson);
    }
}

public class AddPasskeyHandler(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IHttpContextAccessor httpContextAccessor) : IHandle<AddPasskey, AddPasskeyResultDto>
{
    private const int MaxPasskeyCount = 100;

    public async Task<HttpResult<AddPasskeyResultDto>> Handle(AddPasskey request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext?.User!);
        if (user is null) return HttpResult<AddPasskeyResultDto>.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.CredentialJson))
            return Fail("The browser did not provide a passkey.");

        var existing = await userManager.GetPasskeysAsync(user);
        if (existing.Count >= MaxPasskeyCount)
            return Fail("You have reached the maximum number of allowed passkeys.");

        // Verifies the credential against the challenge issued earlier. A
        // credential the server never challenged for is rejected here.
        var attestation = await signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
        if (!attestation.Succeeded)
            return Fail($"Could not add the passkey: {attestation.Failure.Message}");

        var result = await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
        if (!result.Succeeded)
            return Fail("The passkey could not be added to your account.");

        var added = attestation.Passkey;
        return HttpResult<AddPasskeyResultDto>.Ok(new AddPasskeyResultDto(
            true, null,
            new PasskeyDto(Base64Url.EncodeToString(added.CredentialId), added.Name, added.CreatedAt)));
    }

    private static HttpResult<AddPasskeyResultDto> Fail(string error)
        => HttpResult<AddPasskeyResultDto>.Ok(new AddPasskeyResultDto(false, error, null));
}

public class RenamePasskeyHandler(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor httpContextAccessor) : IHandle<RenamePasskey>
{
    public async Task<HttpResult> Handle(RenamePasskey request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext?.User!);
        if (user is null) return HttpResult.Unauthorized();

        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > 200) return HttpResult.BadRequest();

        if (!TryDecode(request.CredentialId, out var credentialId)) return HttpResult.BadRequest();

        // Scoped to this user's passkeys, so a credential id belonging to
        // somebody else simply is not found.
        var passkey = await userManager.GetPasskeyAsync(user, credentialId);
        if (passkey is null) return HttpResult.NotFound();

        passkey.Name = name;
        var result = await userManager.AddOrUpdatePasskeyAsync(user, passkey);
        return result.Succeeded ? HttpResult.NoContent() : HttpResult.BadRequest();
    }

    internal static bool TryDecode(string? value, out byte[] credentialId)
    {
        try
        {
            credentialId = Base64Url.DecodeFromChars(value);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentNullException)
        {
            credentialId = [];
            return false;
        }
    }
}

public class DeletePasskeyHandler(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor httpContextAccessor) : IHandle<DeletePasskey>
{
    public async Task<HttpResult> Handle(DeletePasskey request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext?.User!);
        if (user is null) return HttpResult.Unauthorized();

        if (!RenamePasskeyHandler.TryDecode(request.CredentialId, out var credentialId))
            return HttpResult.BadRequest();

        var result = await userManager.RemovePasskeyAsync(user, credentialId);
        return result.Succeeded ? HttpResult.NoContent() : HttpResult.BadRequest();
    }
}
