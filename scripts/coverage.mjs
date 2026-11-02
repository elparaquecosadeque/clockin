import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  readFileSync,
  realpathSync,
  rmSync,
} from "node:fs";
import { dirname, isAbsolute, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { checkBackendCoverage } from "./coverage-check.mjs";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const targets = process.argv[2] ? [process.argv[2]] : ["backend", "frontend"];
assert(
  process.argv.length <= 3 &&
    targets.every((target) => ["backend", "frontend"].includes(target)),
  "Usage: node scripts/coverage.mjs [backend|frontend]",
);

function run(command, args, cwd = root) {
  const result = spawnSync(command, args, { cwd, stdio: "inherit" });
  if (result.error) console.error(result.error.message);
  return result.status === 0 ? 0 : 1;
}

let failed = 0;
function clearReports(output) {
  if (existsSync(output)) {
    const insideRoot = relative(root, realpathSync(output));
    assert(
      insideRoot && !insideRoot.startsWith("..") && !isAbsolute(insideRoot),
      "Reports must stay inside the repository",
    );
  }
  rmSync(output, { recursive: true, force: true });
}

for (const target of targets) {
  const output = join(root, "coverage", target);
  // Only discard this target's generated reports, so stale hits never inflate a later run.
  assert.equal(relative(root, output), join("coverage", target));
  clearReports(output);
  mkdirSync(output, { recursive: true });
  let input;
  if (target === "backend") {
    failed |= run(
      "dotnet",
      [
        "test",
        "--solution",
        join(root, "backend", "ClockIn.slnx"),
        "--no-restore",
        "--coverage",
        "--coverage-output-format",
        "cobertura",
        "--coverage-settings",
        join(root, "backend", "coverage.config"),
        "--results-directory",
        join(output, "raw"),
      ],
      join(root, "backend"),
    );
    input = join(output, "raw", "*.cobertura.xml");
  } else {
    clearReports(join(root, "frontend", "coverage", "frontend"));
    failed |= run(
      process.execPath,
      [
        join(
          root,
          "frontend",
          "node_modules",
          "@angular",
          "cli",
          "bin",
          "ng.js",
        ),
        "test",
        "--watch=false",
        "--coverage",
      ],
      join(root, "frontend"),
    );
    input = join(root, "frontend", "coverage", "frontend", "lcov.info");
  }
  const generated = run("dotnet", [
    "tool",
    "run",
    "reportgenerator",
    `-reports:${input}`,
    `-targetdir:${output}`,
    "-reporttypes:Html;MarkdownSummaryGithub;JsonSummary",
    `-sourcedirs:${join(root, target)}`,
    "-verbosity:Warning",
  ]);
  failed |= generated;
  if (generated === 0) {
    const report = JSON.parse(
      readFileSync(join(output, "Summary.json"), "utf8"),
    );
    console.log(
      `${target}: ${report.summary.linecoverage}% lines, ${report.summary.branchcoverage}% branches`,
    );
    if (target === "backend") {
      try {
        checkBackendCoverage(report);
      } catch (error) {
        console.error(error.message);
        failed = 1;
      }
    }
    console.log(
      `Reports: ${join(output, "index.html")} and ${join(output, "SummaryGithub.md")}`,
    );
  }
}
process.exitCode = failed;
