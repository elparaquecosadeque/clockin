/** Mirrors the backend's AccessLinkDto. The token is shown once; only its hash is stored. */
export interface AccessLink {
  token: string;
  expiresAt: string;
}
