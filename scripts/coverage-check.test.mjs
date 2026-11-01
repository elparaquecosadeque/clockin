import assert from "node:assert/strict";
import test from "node:test";
import { checkBackendCoverage } from "./coverage-check.mjs";

function report(domain = 80, application = 80) {
  return {
    coverage: {
      assemblies: [
        "Api",
        "Application",
        "Domain",
        "Infrastructure",
        "Persistence",
      ].map((name) => ({
        name: `ClockIn.${name}`,
        coverablelines: 100,
        coveredlines:
          name === "Domain" ? domain : name === "Application" ? application : 1,
      })),
    },
  };
}

test("requires 80% in each core layer, not an average across layers", () => {
  assert.doesNotThrow(() => checkBackendCoverage(report()));
  assert.throws(
    () => checkBackendCoverage(report(79, 100)),
    /Domain line coverage/,
  );
  assert.throws(
    () => checkBackendCoverage(report(100, 79)),
    /Application line coverage/,
  );
});

test("fails if a production layer is missing or has no measured code", () => {
  const missing = report();
  missing.coverage.assemblies.pop();
  assert.throws(
    () => checkBackendCoverage(missing),
    /Missing coverage for ClockIn.Persistence/,
  );
  const empty = report();
  empty.coverage.assemblies[0].coverablelines = 0;
  assert.throws(() => checkBackendCoverage(empty), /No executable lines/);
});

test("uses exact line counts instead of rounded report percentages", () => {
  const rounded = report();
  rounded.coverage.assemblies[2] = {
    name: "ClockIn.Domain",
    coverablelines: 10000,
    coveredlines: 7999,
    coverage: 80,
  };
  assert.throws(() => checkBackendCoverage(rounded), /Domain line coverage/);
});
