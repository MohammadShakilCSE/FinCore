# FinCore frontend

React + TypeScript + Vite. The first screen signs in using the existing backend, then displays the authenticated user's name and email.

## Run locally

1. Set up PostgreSQL and the backend using [authentication setup](../docs/authentication.md).
2. Run the API with its HTTP profile from `backend`: `dotnet run --project src/FinCore.Api --launch-profile http`. Supply the JWT signing key as described in the backend guide.
3. From `fincore-web`, run `npm install`, then `npm run dev`.
4. Open the local URL Vite prints (normally `http://localhost:5173`).

Use an existing registered account. To create a development account, use `POST /api/auth/register` with `name`, `email`, and `password` as shown in `backend/src/FinCore.Api/FinCore.Api.http`. There are no built-in demo credentials.

Vite proxies `/api` to `http://localhost:5196`; change `server.proxy` in `vite.config.ts` if your API uses another address. This avoids requiring backend CORS changes during development.

## Behavior

- Calls `POST /api/auth/login`, then `GET /api/auth/me` with the returned bearer token.
- Holds the session in React memory. Refreshing or closing the page requires another login. Tokens and passwords are never written to browser storage.
- Returns to login on token expiry. Sign-out clears the local session; the backend does not revoke issued tokens.
- Handles invalid credentials, rate limits, invalid responses, unavailable servers, and request timeouts.
- Includes accessible labels, keyboard focus, password visibility, responsive styling, and reduced-motion support.

Account registration, password reset, and a wallet dashboard are not yet frontend screens. Successful login currently shows an account confirmation view.

## Verification and deployment

Run `npm test` for the mocked authentication UI tests, and `npm run build` for TypeScript checking and a production build. `npm run preview` previews the static build; it is not a production server.

Deploy `dist` behind HTTPS and configure your host to forward `/api/*` to the backend on the same origin. Vite's development proxy is not part of the production build. The page optionally loads DM Sans and Manrope from Google Fonts, with local sans-serif fallbacks.

## Project structure

- `src/app`: application entry, router, providers, and public environment config.
- `src/features/auth`: login page, form, API calls, session hook/provider, and auth types.
- `src/features/dashboard/pages/DashboardPage.tsx`: current authenticated account confirmation.
- `src/components`: shared UI primitives and layouts.
- `src/lib`: HTTP wrapper, query client, memory-only token storage, and idempotency keys.
- `src/routes`: guest and protected route guards.
- `src/styles/globals.css`: global styles.
- Remaining feature, component, hook, schema, type, and utility files are explicit scaffolds (`export {}`). They do not provide working registration, wallet, transfer, or history features yet.

`/login` is the guest route; `/` is the protected account view. Production hosting must serve `index.html` for frontend routes while forwarding `/api/*` to the backend.

The `.env`, `.env.development`, and `.env.production` files contain only the public `VITE_API_BASE_URL=/api`. Vite embeds these values in browser code; never put credentials in them. Use ignored `.env.local` files for local overrides. Run `npm run lint` to lint the application.
