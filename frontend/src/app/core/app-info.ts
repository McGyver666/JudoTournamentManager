import { GENERATED_APP_VERSION } from './app-version.generated';

/**
 * Application build metadata surfaced in the shell footer.
 * The value is injected by the release workflow into app-version.generated.ts
 * before the frontend bundle is built, so it matches the deployed release.
 * Local builds fall back to the committed '0.0.0-dev' placeholder.
 */
export const APP_VERSION = GENERATED_APP_VERSION;
