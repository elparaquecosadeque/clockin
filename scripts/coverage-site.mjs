import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import {
  cpSync,
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");

export function coverageIndex(backend, frontend, commit) {
  assert(/^[a-f0-9]{40}$/i.test(commit), "A full source commit is required");
  const values = { COMMIT: commit, SHORT_COMMIT: commit.slice(0, 7) };
  for (const [side, report] of Object.entries({
    BACKEND: backend,
    FRONTEND: frontend,
  })) {
    assert(report.summary.coverablelines > 0, `No measured source in ${side}`);
    for (const [metric, key] of [
      ["LINES", "linecoverage"],
      ["BRANCHES", "branchcoverage"],
    ]) {
      const value = report.summary[key];
      assert(
        value === null ||
          (Number.isFinite(value) && value >= 0 && value <= 100),
        `Invalid ${side} ${metric}`,
      );
      values[`${side}_${metric}`] =
        value === null ? "N/A" : `${value.toFixed(1)}%`;
    }
  }
  values.GENERATED = new Date(
    Math.max(
      Date.parse(backend.summary.generatedon),
      Date.parse(frontend.summary.generatedon),
    ),
  ).toISOString();
  return readFileSync(
    join(root, "docs", "coverage", "index.html"),
    "utf8",
  ).replace(/\{\{([A-Z_]+)\}\}/g, (_, key) => {
    assert(key in values, `Unknown coverage placeholder: ${key}`);
    return values[key];
  });
}

if (
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  const reports = {};
  for (const side of ["backend", "frontend"]) {
    const source = join(root, "coverage", side);
    assert(
      existsSync(join(source, "index.html")),
      `Missing ${side} HTML report`,
    );
    reports[side] = JSON.parse(
      readFileSync(join(source, "Summary.json"), "utf8"),
    );
  }
  const commit =
    process.env.GITHUB_SHA ??
    execFileSync("git", ["rev-parse", "HEAD"], {
      cwd: root,
      encoding: "utf8",
    }).trim();
  const html = coverageIndex(reports.backend, reports.frontend, commit);
  const site = join(root, "coverage", "site");
  mkdirSync(site, { recursive: true });
  for (const side of ["backend", "frontend"]) {
    const source = join(root, "coverage", side);
    for (const entry of readdirSync(source)) {
      if (entry !== "raw")
        cpSync(join(source, entry), join(site, side, entry), {
          recursive: true,
        });
    }
  }
  writeFileSync(join(site, "index.html"), html);
  console.log(`Coverage site: ${join(site, "index.html")}`);
}
