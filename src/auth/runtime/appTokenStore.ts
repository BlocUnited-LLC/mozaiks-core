const APP_TOKEN_KEY = "mozaiks.auth.appToken";

type StorageLike = Pick<Storage, "getItem" | "setItem" | "removeItem">;

const safeStorage = (storage: StorageLike | null | undefined): StorageLike | null => {
  try {
    if (!storage) return null;
    storage.getItem("__mozaiks_test__");
    return storage;
  } catch {
    return null;
  }
};

export const appTokenStore = {
  key: APP_TOKEN_KEY,

  get(): string | null {
    const session = safeStorage(globalThis?.sessionStorage);
    return ((session?.getItem(APP_TOKEN_KEY) || "").trim() || null) as string | null;
  },

  set(token: string): void {
    const trimmed = (token || "").trim();
    const session = safeStorage(globalThis?.sessionStorage);
    if (!trimmed) {
      session?.removeItem(APP_TOKEN_KEY);
      return;
    }
    session?.setItem(APP_TOKEN_KEY, trimmed);
  },

  clear(): void {
    safeStorage(globalThis?.sessionStorage)?.removeItem(APP_TOKEN_KEY);
  },
};

