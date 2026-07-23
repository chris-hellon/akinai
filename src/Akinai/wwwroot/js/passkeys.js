// Passkey registration for the account area.
//
// Both HTTP calls are made HERE, from the browser, rather than through the
// usual typed API client — deliberately.
//
// Registration is stateful: the server issues a challenge and stores it in a
// cookie, then reads and clears that cookie when it verifies the credential.
// Setting a cookie means writing response headers. Under the app's
// InteractiveAuto render mode, a page that is running on Blazor Server calls
// its API handlers in-process, where the "response" is the original page
// request whose headers were sent long ago — so the cookie write throws
// "OnStarting cannot be set because the response has already started".
//
// A fetch from the browser is always a real request with its own response, so
// this path behaves identically whether the page is running on Server or on
// WebAssembly. Listing, renaming and deleting have no such constraint and stay
// on the typed client.

const CREATION_OPTIONS_URL = '/api/account/passkeys/creation-options';
const ADD_URL = '/api/account/passkeys/add';

export function isSupported() {
    return typeof navigator.credentials !== 'undefined'
        && typeof window.PublicKeyCredential !== 'undefined'
        && typeof window.PublicKeyCredential.parseCreationOptionsFromJSON === 'function';
}

async function postJson(url, body) {
    const response = await fetch(url, {
        method: 'POST',
        credentials: 'same-origin',
        headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) {
        throw new Error(`The server responded with status ${response.status}.`);
    }
    return await response.json();
}

// Exported separately so the challenge round-trip can be exercised without
// summoning an authenticator prompt.
export async function getCreationOptions() {
    // The endpoint returns the options as a JSON *string*, so this unwraps twice.
    const optionsString = await postJson(CREATION_OPTIONS_URL);
    return JSON.parse(optionsString);
}

export async function addPasskey() {
    if (!isSupported()) {
        return { succeeded: false, error: 'This browser does not support passkeys. Please update it and try again.', passkey: null };
    }

    let credential;
    try {
        const options = PublicKeyCredential.parseCreationOptionsFromJSON(await getCreationOptions());
        credential = await navigator.credentials.create({ publicKey: options });
    } catch (error) {
        // NotAllowedError covers both "dismissed" and "timed out", and its own
        // message is not worth showing to anyone.
        if (error.name === 'NotAllowedError') {
            return { succeeded: false, error: 'No passkey was provided. The prompt was dismissed or timed out.', passkey: null };
        }
        if (error.name === 'InvalidStateError') {
            return { succeeded: false, error: 'This device already has a passkey registered for your account.', passkey: null };
        }
        return { succeeded: false, error: error.message || 'The passkey could not be created.', passkey: null };
    }

    if (!credential) {
        return { succeeded: false, error: 'The browser did not return a passkey.', passkey: null };
    }

    // Property name matches the contract: the API binds body params by their
    // declared (Pascal-cased) names.
    return await postJson(ADD_URL, { CredentialJson: JSON.stringify(credential) });
}
