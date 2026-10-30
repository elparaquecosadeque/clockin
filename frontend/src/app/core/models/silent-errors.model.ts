import { HttpContextToken } from '@angular/common/http';

/**
 * Rule codes the caller shows itself, so the error interceptor skips its snackbar for them.
 * `'*'` silences every error of the request.
 */
export const SILENT_ERRORS = new HttpContextToken<readonly string[]>(() => []);
