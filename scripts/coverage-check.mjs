import assert from "node:assert/strict";

export function checkBackendCoverage(report) {
  const assemblies = report.coverage.assemblies;
  for (const name of [
    "Api",
    "Application",
    "Domain",
    "Infrastructure",
    "Persistence",
  ]) {
    const assembly = assemblies.find((item) => item.name === `ClockIn.${name}`);
    assert(assembly, `Missing coverage for ClockIn.${name}`);
    assert(
      assembly.coverablelines > 0,
      `No executable lines measured for ClockIn.${name}`,
    );
    assert(
      assembly.coveredlines >= 0 &&
        assembly.coveredlines <= assembly.coverablelines,
      `Invalid line counts for ClockIn.${name}`,
    );
    if (name === "Domain" || name === "Application") {
      const percent = (100 * assembly.coveredlines) / assembly.coverablelines;
      assert(
        percent >= 80,
        `ClockIn.${name} line coverage is ${percent.toFixed(2)}%; at least 80% is required`,
      );
    }
  }
}
