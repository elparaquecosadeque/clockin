import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";

const collection = new URL("../bruno/ClockIn.PublicApi/", import.meta.url);
const read = (name) => readFileSync(new URL(name, collection), "utf8");

function runResponse(name, status, body, values = new Map()) {
  const script = read(name).match(
    /script:post-response \{\r?\n([\s\S]*?)\r?\n\}/,
  )[1];
  vm.runInNewContext(script, {
    res: { getStatus: () => status, getBody: () => body },
    bru: {
      setVar: (key, value) => values.set(key, value),
      deleteVar: (key) => values.delete(key),
    },
  });
  return values;
}

test("Bruno captures runtime auth and the previous company week across Monday's UTC boundary", () => {
  for (const [expiresAt, expected] of [
    ["2026-11-02T05:20:00Z", "2026-10-26"],
    ["2026-11-02T04:20:00Z", "2026-10-19"],
  ]) {
    const values = runResponse("01 Login.bru", 200, {
      accessToken: "test-token",
      expiresAt,
    });
    assert.equal(values.get("accessToken"), "test-token");
    assert.equal(values.get("weekStart"), expected);
    runResponse("01 Login.bru", 422, {}, values);
    assert.equal(values.has("accessToken"), false);
  }
});

test("Bruno edit JSON preserves nullable ends and the latest timesheet version", () => {
  for (const end of [null, "2026-10-20T17:00:00Z"]) {
    const entry = { id: "entry-1", start: "2026-10-20T13:00:00Z", end };
    const values = runResponse("04 Read week.bru", 200, {
      version: 7,
      entries: [entry],
    });
    const template = read("05 Edit entry.bru").match(
      /body:json \{\r?\n([\s\S]*?)\r?\n\}/,
    )[1];
    const body = JSON.parse(
      template.replace(/\{\{(\w+)\}\}/g, (_, key) => values.get(key)),
    );
    assert.equal(values.get("entryId"), entry.id);
    assert.equal(body.version, 7);
    assert.equal(body.start, entry.start);
    assert.equal(body.end, end);
    runResponse("04 Read week.bru", 404, {}, values);
    assert.equal(values.has("entryId"), false);
    assert.equal(values.has("version"), false);
  }
  assert.equal(
    JSON.parse(read("examples/login.json")).email,
    "mia@clockin.demo",
  );
  assert.equal(
    typeof JSON.parse(read("examples/edit-entry.json")).version,
    "number",
  );
});
