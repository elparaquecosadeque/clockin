/** A ProblemDetails response, typed (CODE_STYLE.md, HTTP, session and errors). */
export interface ApiError {
  status: number;
  title: string;
  detail?: string;
  /** The rule code from the specs, on 409 and 422. */
  code?: string;
  /** Field errors on 400, keyed by camelCase field name. */
  fieldErrors: Record<string, string[]>;
}
