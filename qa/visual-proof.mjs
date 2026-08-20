import crypto from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import { chromium } from "playwright";
import AxeBuilder from "@axe-core/playwright";

const baseUrl = (process.env.QA_BASE_URL || "http://web:8080").replace(/\/$/, "");
const mailUrl = (process.env.QA_MAIL_URL || "http://portal:8080/dev/mail").replace(/\/$/, "");
const outputDir = path.join(process.env.QA_OUTPUT_DIR || "/work/artifacts/local", "visual-proof");
const failures = [];
const evidence = [];
const check = (condition, message) => { if (!condition) failures.push(message); };
await fs.mkdir(outputDir, { recursive: true });

const browser = await chromium.launch({ headless: true });
const anonymousRoutes = [
  ["home", "/"], ["recruiters", "/recruiters"], ["privacy", "/privacy"],
  ["register", "/auth/register"], ["sign-in", "/auth/login"], ["admin-sign-in", "/auth/admin"],
  ["complete-login", "/auth/complete"], ["pending", "/auth/pending"], ["access-denied", "/auth/access-denied"]
];
const authenticatedRoutes = [
  ["opportunity", "/portal"], ["admin-dashboard", "/admin"], ["content-dashboard", "/admin/content"],
  ["content-home", "/admin/content/home"], ["preview-home", "/admin/content/preview/home"],
  ["content-settings-umami", "/admin/content/site-settings"], ["preview-settings-umami", "/admin/content/preview/site-settings"],
  ["content-recruiter", "/admin/content/recruiter-profile"], ["preview-recruiter", "/admin/content/preview/recruiter-profile"],
  ["content-opportunity", "/admin/content/opportunity-profile"], ["preview-opportunity", "/admin/content/preview/opportunity-profile"],
  ["content-privacy", "/admin/content/privacy"], ["preview-privacy", "/admin/content/preview/privacy"],
  ["content-discovery", "/admin/content/machine-discovery"], ["preview-discovery", "/admin/content/preview/machine-discovery"],
  ["ai-authoring", "/admin/ai"], ["ai-providers", "/admin/ai/providers"], ["ai-context", "/admin/ai/context"],
  ["recruiter-management", "/admin/recruiters"], ["private-inbox", "/admin/messages"], ["resume-management", "/admin/resume"]
];

async function inspectRoutes(context, routes, sessionLabel) {
  const analyticsRequests = [];
  context.on("request", request => { if (request.url().includes("stats.reneb.au")) analyticsRequests.push(request.url()); });
  for (const viewport of [{ name: "mobile-390x844", width: 390, height: 844 }, { name: "desktop-1440x900", width: 1440, height: 900 }]) {
    const page = await context.newPage();
    await page.setViewportSize({ width: viewport.width, height: viewport.height });
    for (const [name, route] of routes) {
      const response = await page.goto(`${baseUrl}${route}`, { waitUntil: "networkidle" });
      check(response?.status() === 200, `${sessionLabel}/${name}/${viewport.name}: HTTP ${response?.status()}`);
      const metrics = await page.evaluate(() => ({
        clientWidth: document.documentElement.clientWidth,
        scrollWidth: document.documentElement.scrollWidth,
        bodyScrollWidth: document.body.scrollWidth,
        bodyFontSize: Number.parseFloat(getComputedStyle(document.body).fontSize),
        h1Count: document.querySelectorAll("h1").length,
        title: document.title,
        validSummaryVisible: [...document.querySelectorAll(".validation-summary-valid")].some(element => {
          const rect = element.getBoundingClientRect();
          return getComputedStyle(element).display !== "none" && rect.width > 0 && rect.height > 0;
        }),
        editorHeights: [...document.querySelectorAll(".ql-container")].map(element => element.getBoundingClientRect().height),
        editorActionOverlap: (() => {
          const action = document.querySelector("[data-content-editor] > .action-row");
          if (!action) return false;
          const actionTop = action.getBoundingClientRect().top;
          return [...document.querySelectorAll("[data-content-editor] .ql-container")]
            .some(element => element.getBoundingClientRect().bottom > actionTop + 1);
        })(),
        aiFormWidth: document.querySelector(".ai-authoring-grid .form-card")?.getBoundingClientRect().width || null
      }));
      check(metrics.scrollWidth <= metrics.clientWidth, `${sessionLabel}/${name}/${viewport.name}: root overflow ${metrics.scrollWidth}/${metrics.clientWidth}`);
      check(metrics.bodyScrollWidth <= metrics.clientWidth, `${sessionLabel}/${name}/${viewport.name}: body overflow ${metrics.bodyScrollWidth}/${metrics.clientWidth}`);
      check(metrics.bodyFontSize >= 16, `${sessionLabel}/${name}/${viewport.name}: body font ${metrics.bodyFontSize}px`);
      check(metrics.h1Count === 1, `${sessionLabel}/${name}/${viewport.name}: ${metrics.h1Count} h1 elements`);
      check(!metrics.validSummaryVisible, `${sessionLabel}/${name}/${viewport.name}: empty validation summary is visible`);
      check(metrics.editorHeights.every(height => height <= 450), `${sessionLabel}/${name}/${viewport.name}: editor height ${metrics.editorHeights.join(", ")}`);
      check(!metrics.editorActionOverlap, `${sessionLabel}/${name}/${viewport.name}: editor overlaps the action row`);
      if (name === "ai-authoring" && viewport.width >= 1000) check(metrics.aiFormWidth >= 380, `${sessionLabel}/${name}/${viewport.name}: authoring form width ${metrics.aiFormWidth}px`);
      const axe = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"]).analyze();
      const serious = axe.violations.filter(item => ["serious", "critical"].includes(item.impact));
      check(serious.length === 0, `${sessionLabel}/${name}/${viewport.name}: accessibility ${serious.map(item => item.id).join(", ")}`);
      const file = `${sessionLabel}-${name}-${viewport.name}.png`;
      await page.screenshot({ path: path.join(outputDir, file), fullPage: true, animations: "disabled" });
      evidence.push({ session: sessionLabel, page: name, route, viewport: viewport.name, file, ...metrics, seriousAccessibilityViolations: serious.length });
    }
    await page.close();
  }
  if (sessionLabel === "authenticated") check(analyticsRequests.length === 0, `authenticated pages made Umami requests: ${analyticsRequests.join(", ")}`);
}

const anonymous = await browser.newContext();
await anonymous.route("https://stats.reneb.au/**", route => route.fulfill({ status: 200, contentType: "application/javascript", body: "" }));
await inspectRoutes(anonymous, anonymousRoutes, "anonymous");
await anonymous.close();

const admin = await browser.newContext();
const login = await admin.newPage();
await login.setViewportSize({ width: 390, height: 844 });
const existingTokens = new Set();
const existingMail = await login.request.get(mailUrl);
if (existingMail.ok()) {
  for (const message of await existingMail.json()) {
    const existingToken = message.body?.match(/#token=([^"<]+)/)?.[1];
    if (existingToken) existingTokens.add(existingToken);
  }
}
await login.goto(`${baseUrl}/auth/admin`, { waitUntil: "networkidle" });
await login.locator('input[name="Email"]').fill("admin@example.invalid");
await login.getByRole("button", { name: "Send a secure link" }).click();

let token;
for (let attempt = 0; attempt < 20 && !token; attempt += 1) {
  const response = await login.request.get(mailUrl);
  if (response.ok()) {
    const mail = await response.json();
    const messages = mail.filter(item => item.recipient === "admin@example.invalid" && item.subject.includes("secure reneb.au sign-in link"));
    token = messages.map(message => message.body.match(/#token=([^"<]+)/)?.[1]).find(value => value && !existingTokens.has(value));
  }
  if (!token) await new Promise(resolve => setTimeout(resolve, 500));
}
check(Boolean(token), "administrator visual acceptance: magic link was not delivered");
if (token) {
  await login.goto(`${baseUrl}/auth/complete#token=${token}`, { waitUntil: "networkidle" });
  await login.waitForURL(`${baseUrl}/admin/totp`);
  const secret = (await login.locator(".notice-warning code").textContent())?.trim();
  check(Boolean(secret), "administrator visual acceptance: TOTP enrolment key was not shown");
  if (secret) {
    await login.locator(".notice-warning code").evaluate(element => { element.textContent = "[redacted in visual evidence]"; });
    await login.locator(".totp-qr").evaluate(element => { element.style.visibility = "hidden"; });
    await login.screenshot({ path: path.join(outputDir, "authenticated-totp-enrolment-mobile-390x844.png"), fullPage: true, animations: "disabled" });
    const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    let bits = "";
    for (const character of secret.replace(/=+$/, "").toUpperCase()) bits += alphabet.indexOf(character).toString(2).padStart(5, "0");
    const key = Buffer.from(bits.match(/.{8}/g)?.map(value => Number.parseInt(value, 2)) || []);
    const counter = Buffer.alloc(8); counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 1000 / 30)));
    const digest = crypto.createHmac("sha1", key).update(counter).digest();
    const offset = digest[digest.length - 1] & 0x0f;
    const code = ((digest.readUInt32BE(offset) & 0x7fffffff) % 1_000_000).toString().padStart(6, "0");
    await login.locator('input[name="Code"]').fill(code);
    await login.getByRole("button", { name: "Verify administrator access" }).click();
    await login.waitForURL(`${baseUrl}/admin`);
    await inspectRoutes(admin, authenticatedRoutes, "authenticated");
  }
}
await login.close();
await admin.close();
await browser.close();

await fs.writeFile(path.join(outputDir, "visual-proof-report.json"), `${JSON.stringify({ generatedAt: new Date().toISOString(), evidence, failures }, null, 2)}\n`, "utf8");
if (failures.length > 0) {
  console.error(`Visual proof failed with ${failures.length} issue(s):`);
  for (const failure of failures) console.error(`- ${failure}`);
  process.exit(1);
}
console.log(`Visual proof passed for ${evidence.length} route/viewport combinations.`);
