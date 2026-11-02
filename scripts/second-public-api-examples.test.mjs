import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";

const collection = new URL(
  "../bruno/ClockIn.SecondPublicApi/",
  import.meta.url,
);
const read = (name) => readFileSync(new URL(name, collection), "utf8");

function run(name, kind, status, body, values = new Map()) {
  const script = read(name).match(
    new RegExp(`script:${kind} \\{\\r?\\n([\\s\\S]*?)\\r?\\n\\}`),
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

test("Bruno captures tokens only at runtime and clears rejected sign-ins", () => {
  const values = run("03 Login Ada.bru", "post-response", 200, {
    accessToken: "admin-token",
  });
  assert.equal(values.get("adminToken"), "admin-token");
  run("03 Login Ada.bru", "post-response", 422, {}, values);
  assert.equal(values.has("adminToken"), false);
  run(
    "06 Login supervisor.bru",
    "post-response",
    200,
    { accessToken: "user-token" },
    values,
  );
  assert.equal(values.get("accessToken"), "user-token");
  run("06 Login supervisor.bru", "post-response", 422, {}, values);
  assert.equal(values.has("accessToken"), false);
});

test("Bruno generates repeatable creation emails and captures activation responses", () => {
  const values = new Map([["activationToken", "stale"]]);
  run("04 Create supervisor.bru", "pre-request", 0, {}, values);
  assert.match(
    values.get("supervisorEmail"),
    /^bruno-supervisor-.+@clockin\.demo$/,
  );
  assert.equal(values.has("activationToken"), false);
  const previous = values.get("supervisorEmail");
  run("04 Create supervisor.bru", "pre-request", 0, {}, values);
  assert.notEqual(values.get("supervisorEmail"), previous);
  run(
    "04 Create supervisor.bru",
    "post-response",
    200,
    { token: "activation-secret" },
    values,
  );
  assert.equal(values.get("activationToken"), "activation-secret");
});

test("Employee examples use Office's current ID and reject a missing Office example", () => {
  const values = new Map();
  const document = {
    paths: {
      "/api/v1/users": {
        post: {
          requestBody: {
            content: {
              "application/json": {
                examples: { Employee: { value: { teamId: "office-id" } } },
              },
            },
          },
        },
      },
    },
  };
  run(
    "Employee/01 Load Office example.bru",
    "post-response",
    200,
    document,
    values,
  );
  assert.equal(values.get("officeTeamId"), "office-id");
  run("Employee/02 Create employee.bru", "pre-request", 0, {}, values);
  assert.match(
    values.get("employeeEmail"),
    /^bruno-employee-.+@clockin\.demo$/,
  );
  const template = read("Employee/02 Create employee.bru").match(
    /body:json \{\r?\n([\s\S]*?)\r?\n\}/,
  )[1];
  const body = JSON.parse(
    template.replace(/\{\{(\w+)\}\}/g, (_, key) => values.get(key)),
  );
  assert.equal(body.teamId, "office-id");
  assert.equal(body.role, "Employee");
  delete document.paths["/api/v1/users"].post.requestBody.content[
    "application/json"
  ].examples.Employee;
  assert.throws(
    () =>
      run(
        "Employee/01 Load Office example.bru",
        "post-response",
        200,
        document,
        values,
      ),
    /Office is unavailable/,
  );
  assert.equal(values.has("officeTeamId"), false);
});
