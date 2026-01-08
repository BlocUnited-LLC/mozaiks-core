const LOCAL_TOKEN_KEY = "mozaiks.auth.localToken";

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

export const localTokenStore = {
  key: LOCAL_TOKEN_KEY,

  get(): string | null {
    const session = safeStorage(globalThis?.sessionStorage);
    const local = safeStorage(globalThis?.localStorage);
    return ((session?.getItem(LOCAL_TOKEN_KEY) || "").trim() || (local?.getItem(LOCAL_TOKEN_KEY) || "").trim() || null) as
      | string
      | null;
  },

  set(token: string, remember: boolean): void {
    const trimmed = (token || "").trim();
    const session = safeStorage(globalThis?.sessionStorage);
    const local = safeStorage(globalThis?.localStorage);

    if (!trimmed) {
      session?.removeItem(LOCAL_TOKEN_KEY);
      local?.removeItem(LOCAL_TOKEN_KEY);
      return;
    }

    if (remember) {
      local?.setItem(LOCAL_TOKEN_KEY, trimmed);
      session?.removeItem(LOCAL_TOKEN_KEY);
      return;
    }

    session?.setItem(LOCAL_TOKEN_KEY, trimmed);
    local?.removeItem(LOCAL_TOKEN_KEY);
  },

  clear(): void {
    safeStorage(globalThis?.sessionStorage)?.removeItem(LOCAL_TOKEN_KEY);
    safeStorage(globalThis?.localStorage)?.removeItem(LOCAL_TOKEN_KEY);
  },
};

