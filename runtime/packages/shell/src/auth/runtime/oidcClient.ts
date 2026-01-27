import { UserManager, WebStorageStateStore, User } from "oidc-client-ts";
import { getOidcConfig } from "./authConfig";

let userManager: UserManager | null = null;

const getUserManager = (): UserManager => {
  if (userManager) return userManager;

  const config = getOidcConfig();
  if (!config.ok) {
    throw new Error(config.error);
  }

  userManager = new UserManager({
    authority: config.config.authority,
    client_id: config.config.clientId,
    redirect_uri: config.config.redirectUri,
    post_logout_redirect_uri: config.config.postLogoutRedirectUri,
    response_type: "code",
    scope: config.config.scope,
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    filterProtocolClaims: true,
    loadUserInfo: false,
  });

  return userManager;
};

/**
 * Extract the subject (sub) claim from the current user's ID token.
 * 
 * SECURITY NOTE: This is for routing hints only.
 * The server MUST derive user identity from the JWT, not trust client-supplied values.
 * MozaiksAI will validate that any path-based user_id matches the JWT sub claim.
 * 
 * @returns The sub claim from the ID token, or null if unavailable
 */
const getTokenSubject = async (): Promise<string | null> => {
  try {
    const manager = getUserManager();
    const user = await manager.getUser();
    if (!user || user.expired) return null;
    
    // The sub claim is in the profile (decoded ID token claims)
    // oidc-client-ts exposes this via user.profile.sub
    return user.profile?.sub || null;
  } catch {
    return null;
  }
};

export const oidc = {
  async signinRedirect(returnTo?: string): Promise<void> {
    const manager = getUserManager();
    await manager.signinRedirect({
      state: { returnTo: (returnTo || "").trim() || undefined },
    });
  },

  async handleSigninCallback(): Promise<{ accessToken: string | null; returnTo?: string }> {
    const manager = getUserManager();
    const user = await manager.signinRedirectCallback();
    const returnTo = (user?.state as any)?.returnTo;
    return { accessToken: user?.access_token || null, returnTo: typeof returnTo === "string" ? returnTo : undefined };
  },

  async getAccessToken(): Promise<string | null> {
    try {
      const manager = getUserManager();
      const user = await manager.getUser();
      if (!user) return null;
      if (user.expired) {
        await manager.removeUser();
        return null;
      }
      return user.access_token || null;
    } catch {
      return null;
    }
  },

  async signoutRedirect(): Promise<void> {
    try {
      const manager = getUserManager();
      await manager.signoutRedirect();
    } catch {
      try {
        const manager = getUserManager();
        await manager.removeUser();
      } catch {
        // ignore
      }
      const cfg = getOidcConfig();
      window.location.assign(cfg.ok ? cfg.config.postLogoutRedirectUri : "/");
    }
  },

  async removeUser(): Promise<void> {
    try {
      const manager = getUserManager();
      await manager.removeUser();
    } catch {
      // ignore
    }
  },

  /**
   * Get the subject (sub) claim from the current user's ID token.
   * 
  * SECURITY: This is for routing hints only.
   * Server MUST derive identity from JWT, not client-supplied values.
   */
  async getTokenSubject(): Promise<string | null> {
    return await getTokenSubject();
  },
};
