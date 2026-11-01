/** A server-filtered page; at most 50 items. */
export interface ReportPage<T> {
  items: T[];
  hasMore: boolean;
}
