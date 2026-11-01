import assert from "node:assert/strict";
import test from "node:test";
import { coverageIndex } from "./coverage-site.mjs";

const commit = "1234567890abcdef1234567890abcdef12345678";
const report = {
  summary: {
    coverablelines: 100,
    linecoverage: 81.2,
    branchcoverage: 72.3,
    generatedon: "2026-11-01T17:00:00Z",
  },
};

test("links both reports and records measured coverage, date and source commit", () => {
  const html = coverageIndex(report, report, commit);
  assert.match(html, /href="backend\/index.html"/);
  assert.match(html, /href="frontend\/index.html"/);
  assert.match(html, /81.2%/);
  assert.match(html, /72.3%/);
  assert.match(html, /2026-11-01T17:00:00.000Z/);
  assert(html.includes(`/commit/${commit}`));
  assert(!html.includes("{{"));
});

test("fails closed for missing metrics, empty reports or an invalid commit", () => {
  assert.throws(
    () => coverageIndex(report, report, "<script>"),
    /source commit/,
  );
  assert.throws(
    () => coverageIndex(report, { summary: {} }, commit),
    /No measured source/,
  );
  assert.throws(
    () =>
      coverageIndex(
        report,
        { summary: { ...report.summary, linecoverage: 101 } },
        commit,
      ),
    /Invalid FRONTEND LINES/,
  );
});

test("does not invent a branch percentage when the report has no branches", () => {
  const html = coverageIndex(
    report,
    { summary: { ...report.summary, branchcoverage: null } },
    commit,
  );
  assert.match(html, /N\/A/);
});
