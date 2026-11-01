import assert from "node:assert/strict";

const base = process.argv[2] ?? "https://clockin.gerardoleon.dev";
const discovery = await fetch(`${base}/api/demo/accounts`);
assert.equal(discovery.status, 200);
const demo = await discovery.json();

for (const email of [
  "ada@clockin.demo",
  "sam@clockin.demo",
  "eve@clockin.demo",
]) {
  assert(demo.accounts.some((account) => account.email === email));
  const login = await fetch(`${base}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password: demo.password }),
  });
  assert.equal(login.status, 200, `Sign-in failed for ${email}`);
  const cookie = login.headers.get("set-cookie");
  assert.match(cookie, /HttpOnly/i);
  assert.match(cookie, /Secure/i);
  const token = await login.json();
  const me = await fetch(`${base}/api/me`, {
    headers: { Authorization: `Bearer ${token.accessToken}` },
  });
  assert.equal(me.status, 200);
  assert.equal((await me.json()).email, email);
  const refresh = await fetch(`${base}/api/auth/refresh`, {
    method: "POST",
    headers: { Cookie: cookie.split(";")[0] },
  });
  assert.equal(refresh.status, 200, `Refresh failed for ${email}`);
  const logout = await fetch(`${base}/api/auth/logout`, {
    method: "POST",
    headers: { Cookie: refresh.headers.get("set-cookie").split(";")[0] },
  });
  assert.equal(logout.status, 204);
}

console.log(
  "Demo discovery, admin/supervisor/employee sign-in, secure cookies and refresh passed.",
);
