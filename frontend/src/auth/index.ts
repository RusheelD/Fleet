export {
  msalInstance,
  apiLoginRequest,
  emailLoginRequest,
  emailSignUpRequest,
  googleLoginRequest,
  microsoftLoginRequest,
  redirectUri,
  authConfigError,
  isAuthConfigured,
} from './msalConfig'
export type { AuthLoginProvider } from './msalConfig'
export {
  LOGIN_PROVIDER_LINK_ERROR_KEY,
  PENDING_LOGIN_PROVIDER_LINK_STATE_KEY,
} from './loginProviderLinkStorage'
