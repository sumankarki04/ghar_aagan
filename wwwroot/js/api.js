// API layer for Ghar Aagan — centralized fetch with typed errors, timeout,
// cancellation, retry-with-backoff (safe GETs only), and request deduplication.
const API = (() => {
    const TOKEN_KEY = "ga_token";
    const USER_KEY = "ga_user";
    const TIMEOUT_MS = 15000;

    // Typed error so callers can branch on HTTP status / payload.
    class ApiError extends Error {
        constructor(message, status, payload = null) {
            super(message);
            this.name = "ApiError";
            this.status = status;
            this.payload = payload;
        }
    }

    const getToken = () => localStorage.getItem(TOKEN_KEY);
    const getUser = () => JSON.parse(localStorage.getItem(USER_KEY) || "null");
    const setSession = (auth) => {
        localStorage.setItem(TOKEN_KEY, auth.token);
        localStorage.setItem(USER_KEY, JSON.stringify({
            id: auth.userId, name: auth.fullName, email: auth.email, role: auth.role
        }));
    };
    const clearSession = () => {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(USER_KEY);
    };

    const sleep = (ms) => new Promise(r => setTimeout(r, ms));

    // One raw fetch attempt with a timeout. Honors an optional caller signal too.
    async function attempt(method, path, body, signal) {
        const timer = new AbortController();
        const onAbort = () => timer.abort();
        if (signal) {
            if (signal.aborted) timer.abort();
            else signal.addEventListener("abort", onAbort, { once: true });
        }
        const timeout = setTimeout(() => timer.abort(), TIMEOUT_MS);

        try {
            const headers = { "Content-Type": "application/json" };
            const token = getToken();
            if (token) headers["Authorization"] = "Bearer " + token;

            const res = await fetch("/api" + path, {
                method, headers,
                body: body ? JSON.stringify(body) : undefined,
                signal: timer.signal
            });

            if (res.status === 204) return null;

            const text = await res.text();
            let data = null;
            try { data = text ? JSON.parse(text) : null; } catch { data = text; }

            if (!res.ok) {
                const message = (data && data.title) || (typeof data === "string" && data)
                    || `Request failed (${res.status})`;
                throw new ApiError(message, res.status, data);
            }
            return data;
        } catch (err) {
            // A timeout aborts via our controller; distinguish it from a caller cancel.
            if (err.name === "AbortError" && !(signal && signal.aborted))
                throw new ApiError("Request timed out. Please try again.", 408);
            throw err;
        } finally {
            clearTimeout(timeout);
            if (signal) signal.removeEventListener("abort", onAbort);
        }
    }

    // Retry transient failures only: network errors or 5xx. Never 4xx, never aborts.
    async function withBackoff(fn, retries = 2, delay = 300) {
        try {
            return await fn();
        } catch (err) {
            const isAbort = err.name === "AbortError";
            const status = err instanceof ApiError ? err.status : null;
            const retryable = !isAbort && (status === null || status >= 500);
            if (retries <= 0 || !retryable) throw err;
            await sleep(delay + Math.random() * 100);
            return withBackoff(fn, retries - 1, delay * 2);
        }
    }

    // Dedupe identical in-flight GETs so parallel callers share one request.
    const inFlight = new Map();

    async function request(method, path, body, opts = {}) {
        const signal = opts.signal;

        if (method === "GET") {
            const key = "GET " + path;
            if (inFlight.has(key) && !signal) return inFlight.get(key);
            const p = withBackoff(() => attempt("GET", path, null, signal))
                .finally(() => inFlight.delete(key));
            if (!signal) inFlight.set(key, p);
            return p;
        }

        // Mutations: no retry (not idempotent), no dedupe.
        return attempt(method, path, body, signal);
    }

    return {
        ApiError, getToken, getUser, setSession, clearSession,
        get: (p, opts) => request("GET", p, null, opts),
        post: (p, b, opts) => request("POST", p, b, opts),
        put: (p, b, opts) => request("PUT", p, b, opts),
        del: (p, opts) => request("DELETE", p, null, opts),
    };
})();
