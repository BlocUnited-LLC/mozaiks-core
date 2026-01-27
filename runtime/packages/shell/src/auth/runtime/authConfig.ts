export type AuthMode = "platform" | "external" | "local";

type RuntimeAuthConfig = Partial<{
  authority: string;
  clientId: string;
  redirectUri: string;
  postLogoutRedirectUri: string;
  scope: string;
  apiScope: string;
  tokenExchangeEnabled: boolean;
}>;

const readEnv = (name: string): string | undefined => {
  try {
    const env = (globalThis as any)?.import?.meta?.env as Record<string, string | undefined> | undefined;
    const value = env?.[name];
    return typeof value === "string" && value.trim() ? value.trim() : undefined;
  } catch {
    return undefined;
  }
};

const readRuntime = (): RuntimeAuthConfig => {
  const w = globalThis as any;
  const config = w?.__MOZAIKS_AUTH_CONFIG;
  return config && typeof config === "object" ? (config as RuntimeAuthConfig) : {};
};

const parseBool = (value: unknown): boolean | undefined => {
  if (typeof value === "boolean") return value;
  if (typeof value !== "string") return undefined;
  const normalized = value.trim().toLowerCase();
  if (!normalized) return undefined;
  if (["1", "true", "yes", "y", "on"].includes(normalized)) return true;
  if (["0", "false", "no", "n", "off"].includes(normalized)) return false;
  return undefined;
};

/**
 * Get the current authentication mode.
 * 
 * SECURITY INVARIANTS:
 * - Default is "external" (OIDC-based) — local mode is OPT-IN only
 * - Platform mode is for Mozaiks-hosted deployments (CIAM-only)
 * - Local mode is for offline/self-hosted development ONLY
 * - Platform/external modes NEVER use local identity paths
 * 
 * @returns {AuthMode} The configured auth mode
 */
export const getAuthMode = (): AuthMode => {
  const w = globalThis as any;
  const runtimeMode = typeof w?.__MOZAIKS_AUTH_MODE === "string" ? w.__MOZAIKS_AUTH_MODE : undefined;
  const raw =
    (runtimeMode || "").trim() ||
    readEnv("VITE_MOZAIKS_AUTH_MODE") ||
    readEnv("VITE_AUTH_MODE") ||
    "external"; // DEFAULT: OIDC-based auth (not local)

  const mode = raw.trim().toLowerCase();
  
  // SECURITY: Only accept explicit mode values
  if (mode === "platform" || mode === "external" || mode === "local") {
    // Log warning in development if local mode is enabled
    if (mode === "local" && typeof console !== "undefined") {
      console.warn(
        "⚠️ MOZAIKS_AUTH_MODE=local is enabled. This mode is for development/self-host only. " +
        "Production deployments should use 'platform' or 'external'."
      );
    }
    return mode;
  }

  // Compatibility aliases (deprecated).
  if (mode === "oidc") return "external";

  // DEFAULT: Fall back to external (OIDC) — never default to local
  return "external";
};

export type OidcConfig = {
  authority: string;
  clientId: string;
  redirectUri: string;
  postLogoutRedirectUri: string;
  scope: string;
};

export const getOidcConfig = ():
  | { ok: true; config: OidcConfig }
  | { ok: false; error: string; config: Partial<OidcConfig> } => {
  const runtime = readRuntime();

  const authority = (runtime.authority || readEnv("VITE_AUTH_AUTHORITY") || "").trim();
  const clientId = (runtime.clientId || readEnv("VITE_AUTH_CLIENT_ID") || "").trim();

  const origin = typeof window !== "undefined" && window.location ? window.location.origin : "";
  const redirectUri = (runtime.redirectUri || readEnv("VITE_AUTH_REDIRECT_URI") || `${origin}/auth/callback`).trim();
  const postLogoutRedirectUri = (
    runtime.postLogoutRedirectUri ||
    readEnv("VITE_AUTH_POST_LOGOUT_REDIRECT_URI") ||
    `${origin}/`
  ).trim();

  const baseScope = (runtime.scope || readEnv("VITE_AUTH_SCOPE") || "openid profile email").trim();
  const apiScope = (runtime.apiScope || readEnv("VITE_AUTH_API_SCOPE") || "").trim();
  const scope = apiScope ? `${baseScope} ${apiScope}` : baseScope;

  const config: Partial<OidcConfig> = {
    authority,
    clientId,
    redirectUri,
    postLogoutRedirectUri,
    scope,
  };

  if (!authority) return { ok: false, error: "Missing VITE_AUTH_AUTHORITY", config };
  if (!clientId) return { ok: false, error: "Missing VITE_AUTH_CLIENT_ID", config };
  if (!redirectUri) return { ok: false, error: "Missing VITE_AUTH_REDIRECT_URI", config };

  return {
    ok: true,
    config: config as OidcConfig,
  };
};

export const isTokenExchangeEnabled = (): boolean => {
  const runtime = readRuntime();
  const runtimeFlag = parseBool((runtime as any)?.tokenExchangeEnabled);
  if (runtimeFlag !== undefined) return runtimeFlag;

  const w = globalThis as any;
  const windowFlag = parseBool(w?.__MOZAIKS_TOKEN_EXCHANGE);
  if (windowFlag !== undefined) return windowFlag;

  const envFlag = parseBool(readEnv("VITE_MOZAIKS_TOKEN_EXCHANGE") || readEnv("VITE_TOKEN_EXCHANGE"));
  return envFlag ?? false;
};
