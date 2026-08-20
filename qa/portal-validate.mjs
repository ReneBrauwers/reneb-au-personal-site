import fs from "node:fs/promises";
import path from "node:path";
import { chromium, devices, webkit } from "playwright";
import AxeBuilder from "@axe-core/playwright";

const baseUrl = (process.env.QA_BASE_URL || "http://web:8080").replace(/\/$/, "");
const mailUrl = (process.env.QA_MAIL_URL || "http://portal:8080/dev/mail").replace(/\/$/, "");
const outputDir = process.env.QA_OUTPUT_DIR || "/work/artifacts/local";
const analyticsUrl = "https://stats.reneb.au/script.js";
const privatePatterns = [
  { label: "Australian-dollar amount", expression: /A\$\s*\d/i },
  { label: "daily-rate language", expression: /\bper\s+day\b|\/day\b/i },
  { label: "private transition-payment language", expression: /sign-on|transition payment/i }
];
const viewports = [
  { name: "320x568", width: 320, height: 568 },
  { name: "375x667", width: 375, height: 667 },
  { name: "390x844", width: 390, height: 844 },
  { name: "768x1024", width: 768, height: 1024 },
  { name: "1440x900", width: 1440, height: 900 }
];

const failures = [];
const results = [];
const check = (condition, message) => { if (!condition) failures.push(message); };
await fs.mkdir(outputDir, { recursive: true });

const browser = await chromium.launch({ headless: true });
for (const viewport of viewports) {
  const context = await browser.newContext({ viewport: { width: viewport.width, height: viewport.height } });
  await context.route(analyticsUrl, route => route.fulfill({ status: 200, contentType: "application/javascript", body: "" }));
  const page = await context.newPage();
  const response = await page.goto(`${baseUrl}/recruiters`, { waitUntil: "networkidle" });
  check(response?.status() === 200, `${viewport.name}: recruiter page returned ${response?.status()}`);

  const metrics = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
    bodyScrollWidth: document.body.scrollWidth,
    rootOverflow: getComputedStyle(document.documentElement).overflowX,
    bodyOverflow: getComputedStyle(document.body).overflowX,
    rootOverscroll: getComputedStyle(document.documentElement).overscrollBehaviorX,
    bodyOverscroll: getComputedStyle(document.body).overscrollBehaviorX,
    bodyFontSize: Number.parseFloat(getComputedStyle(document.body).fontSize),
    h1Count: document.querySelectorAll("h1").length,
    analytics: [...document.querySelectorAll('script[src="https://stats.reneb.au/script.js"]')].map(script => ({
      websiteId: script.dataset.websiteId,
      domains: script.dataset.domains,
      excludeSearch: script.dataset.excludeSearch,
      doNotTrack: script.dataset.doNotTrack
    })),
    targets: [...document.querySelectorAll("a, button, input")].filter(element => {
      const rect = element.getBoundingClientRect();
      return rect.width > 0 && rect.height > 0;
    }).map(element => {
      const target = element.matches('input[type="checkbox"], input[type="radio"]') ? element.closest("label") || element : element;
      const rect = target.getBoundingClientRect();
      return { label: element.textContent.trim() || element.getAttribute("aria-label") || element.tagName, width: rect.width, height: rect.height };
    }),
    text: document.body.innerText
  }));

  check(metrics.scrollWidth <= metrics.clientWidth, `${viewport.name}: root horizontal overflow ${metrics.scrollWidth}/${metrics.clientWidth}`);
  check(metrics.bodyScrollWidth <= metrics.clientWidth, `${viewport.name}: body horizontal overflow ${metrics.bodyScrollWidth}/${metrics.clientWidth}`);
  check(["clip", "hidden"].includes(metrics.rootOverflow), `${viewport.name}: root overflow-x is ${metrics.rootOverflow}`);
  check(["clip", "hidden"].includes(metrics.bodyOverflow), `${viewport.name}: body overflow-x is ${metrics.bodyOverflow}`);
  check(metrics.rootOverscroll === "none" && metrics.bodyOverscroll === "none", `${viewport.name}: horizontal overscroll is ${metrics.rootOverscroll}/${metrics.bodyOverscroll}`);
  check(metrics.bodyFontSize >= 18, `${viewport.name}: body font is ${metrics.bodyFontSize}px`);
  check(metrics.h1Count === 1, `${viewport.name}: found ${metrics.h1Count} h1 elements`);
  check(metrics.analytics.length === 1, `${viewport.name}: expected one Umami tracker`);
  check(metrics.analytics[0]?.websiteId === "55c627ba-826f-4472-9479-f1279071488c", `${viewport.name}: Umami website id mismatch`);
  check(metrics.analytics[0]?.domains === "reneb.au", `${viewport.name}: Umami domain restriction mismatch`);
  check(metrics.analytics[0]?.excludeSearch === "true" && metrics.analytics[0]?.doNotTrack === "true", `${viewport.name}: privacy tracker flags missing`);
  for (const pattern of privatePatterns) check(!pattern.expression.test(metrics.text), `${viewport.name}: ${pattern.label} leaked publicly`);
  for (const target of metrics.targets) check(target.width >= 44 && target.height >= 44, `${viewport.name}: target '${target.label}' is ${Math.round(target.width)}x${Math.round(target.height)}`);

  const axe = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"]).analyze();
  const serious = axe.violations.filter(violation => ["serious", "critical"].includes(violation.impact));
  check(serious.length === 0, `${viewport.name}: serious accessibility issues ${serious.map(item => item.id).join(", ")}`);
  await page.screenshot({ path: path.join(outputDir, `recruiter-${viewport.name}.png`), fullPage: true, animations: "disabled" });
  results.push({ viewport: viewport.name, ...metrics, axeViolations: axe.violations.length });
  await context.close();
}

for (const browserName of ["chromium", "webkit"]) {
  const touchBrowser = browserName === "chromium" ? browser : await webkit.launch({ headless: true });
  const context = await touchBrowser.newContext({ ...devices["iPhone 13"], colorScheme: "light" });
  await context.route(analyticsUrl, route => route.fulfill({ status: 200, contentType: "application/javascript", body: "" }));
  const page = await context.newPage();
  await page.goto(`${baseUrl}/recruiters`, { waitUntil: "networkidle" });
  const movement = await page.evaluate(() => {
    document.documentElement.scrollLeft = 10000;
    document.body.scrollLeft = 10000;
    return {
      windowX: window.scrollX,
      rootX: document.documentElement.scrollLeft,
      bodyX: document.body.scrollLeft,
      clientWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth
    };
  });
  check(movement.windowX === 0 && movement.rootX === 0 && movement.bodyX === 0, `${browserName} touch moved horizontally: ${JSON.stringify(movement)}`);
  check(movement.scrollWidth <= movement.clientWidth, `${browserName} touch overflow ${movement.scrollWidth}/${movement.clientWidth}`);
  await context.close();
  if (browserName === "webkit") await touchBrowser.close();
}

const request = await browser.newPage();
for (const route of ["/recruiters", "/llms.txt", "/recruiters/profile.md", "/candidate.json"]) {
  const response = await request.request.get(`${baseUrl}${route}`);
  const body = await response.text();
  check(response.status() === 200, `${route}: returned ${response.status()}`);
  check(body.toLowerCase().includes("enterprise architecture"), `${route}: canonical evidence missing`);
  for (const pattern of privatePatterns) check(!pattern.expression.test(body), `${route}: ${pattern.label} leaked`);
}
const candidateResponse = await request.request.get(`${baseUrl}/candidate.json`);
check(candidateResponse.status() === 200, `candidate.json contract response is ${candidateResponse.status()}`);
if (candidateResponse.status() === 200) {
  const candidate = await candidateResponse.json();
  check(candidate.schemaVersion === "1.0", `candidate.json: schemaVersion is ${candidate.schemaVersion}`);
  check(candidate.candidateSupplied === true, "candidate.json: candidateSupplied flag missing");
}

const register = await request.request.get(`${baseUrl}/auth/register`);
const registerBody = await register.text();
check(register.status() === 200, `/auth/register returned ${register.status()}`);
check(!registerBody.includes("stats.reneb.au"), "/auth/register loads analytics");
check((register.headers()["cache-control"] || "").includes("no-store"), "/auth/register is cacheable");
check((register.headers()["x-robots-tag"] || "").includes("noindex"), "/auth/register can be indexed");

const privacy = await request.request.get(`${baseUrl}/privacy`);
const privacyBody = await privacy.text();
check(privacy.status() === 200, `/privacy returned ${privacy.status()}`);
check(privacyBody.includes("Australian Privacy Principles"), "/privacy omits its privacy baseline");
check(!privacyBody.includes("stats.reneb.au"), "/privacy loads analytics");

const robots = await request.request.get(`${baseUrl}/robots.txt`);
const robotsBody = await robots.text();
check(robots.status() === 200, `/robots.txt returned ${robots.status()}`);
check(/^Disallow: \/portal$/m.test(robotsBody), "/robots.txt does not disallow the exact /portal route");

const portal = await request.request.get(`${baseUrl}/portal`, { maxRedirects: 0 });
check(portal.status() === 302, `/portal anonymous response is ${portal.status()}`);
check((portal.headers()["location"] || "").includes("/auth/login"), `/portal redirect is ${portal.headers()["location"]}`);
check((portal.headers()["x-robots-tag"] || "").includes("noindex"), "/portal redirect can be indexed");

const authContext = await browser.newContext({ viewport: { width: 320, height: 568 } });
const authPage = await authContext.newPage();
const qaEmail = `qa-${Date.now()}@executivesearch.example`;
await authPage.goto(`${baseUrl}/auth/register`, { waitUntil: "networkidle" });
await authPage.locator('input[name="Input.Name"]').fill("QA Executive Recruiter");
await authPage.locator('input[name="Input.Email"]').fill(qaEmail);
await authPage.locator('input[name="Input.Organisation"]').fill("Executive Search QA");
await authPage.locator('input[name="Input.Title"]').fill("Search Partner");
await authPage.locator('input[name="Input.ProfileUrl"]').fill("https://executivesearch.example");
await authPage.locator('input[name="Input.Country"]').fill("Australia");
await authPage.locator('textarea[name="Input.Purpose"]').fill("Sourcing a senior enterprise architecture mandate with real design authority and executive accountability.");
await authPage.locator('input[name="Input.PrivacyAccepted"][type="checkbox"]').check();
await authPage.getByRole("button", { name: "Send my verification link" }).click();
await authPage.getByRole("heading", { name: "Check your email" }).waitFor();

let token;
for (let attempt = 0; attempt < 20 && !token; attempt += 1) {
    const mailResponse = await authPage.request.get(mailUrl);
  if (mailResponse.ok()) {
    const messages = await mailResponse.json();
    const message = messages.find(item => item.recipient === qaEmail);
    token = message?.body.match(/#token=([^"<]+)/)?.[1];
  }
  if (!token) await new Promise(resolve => setTimeout(resolve, 500));
}
check(Boolean(token), "browser authentication: development magic link was not delivered");
if (token) {
  await authPage.goto(`${baseUrl}/auth/complete#token=${token}`, { waitUntil: "networkidle" });
  check(new URL(authPage.url()).pathname === "/portal", `browser authentication redirected to ${authPage.url()}`);
  check((await authPage.locator("body").innerText()).includes("Where the mandate and terms become specific"), "browser authentication did not establish a private session");
  check((await authPage.locator('script[src*="stats.reneb.au"]').count()) === 0, "authenticated portal loads analytics");
  const authenticatedMetrics = await authPage.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
    bodyScrollWidth: document.body.scrollWidth,
    targets: [...document.querySelectorAll("a, button, input")].filter(element => {
      const rect = element.getBoundingClientRect();
      return rect.width > 0 && rect.height > 0;
    }).map(element => {
      const target = element.matches('input[type="checkbox"], input[type="radio"]') ? element.closest("label") || element : element;
      const rect = target.getBoundingClientRect();
      return { label: element.textContent.trim() || element.getAttribute("aria-label") || element.tagName, width: rect.width, height: rect.height };
    })
  }));
  check(authenticatedMetrics.scrollWidth <= authenticatedMetrics.clientWidth, `authenticated 320x568: root horizontal overflow ${authenticatedMetrics.scrollWidth}/${authenticatedMetrics.clientWidth}`);
  check(authenticatedMetrics.bodyScrollWidth <= authenticatedMetrics.clientWidth, `authenticated 320x568: body horizontal overflow ${authenticatedMetrics.bodyScrollWidth}/${authenticatedMetrics.clientWidth}`);
  for (const target of authenticatedMetrics.targets) check(target.width >= 44 && target.height >= 44, `authenticated 320x568: target '${target.label}' is ${Math.round(target.width)}x${Math.round(target.height)}`);

  await authPage.setViewportSize({ width: 390, height: 844 });
  const lensEntryMetrics = await authPage.evaluate(() => {
    const section = document.querySelector('.mandate-lens');
    const action = document.querySelector('.lens-action .button');
    return {
      sectionTop: section?.getBoundingClientRect().top || 0,
      actionTop: action?.getBoundingClientRect().top || 0
    };
  });
  const lensEntryDistance = Math.round(lensEntryMetrics.actionTop - lensEntryMetrics.sectionTop);
  check(lensEntryDistance <= 844, `Mandate Lens 390x844: Run action begins ${lensEntryDistance}px after the section starts`);

  await authPage.locator('input[name="MandateRole"]').fill("Synthetic QA mandate — not a real opportunity");
  await authPage.locator('textarea[name="MandateText"]').first().fill("Synthetic acceptance scenario: Group Chief Architect with enterprise design authority for a regulated financial-services transformation, owning investment roadmaps, responsible AI governance and the connection between Product and Engineering delivery.");
  await authPage.getByRole("button", { name: "Run Mandate Lens" }).click();
  await authPage.getByRole("heading", { name: "This mandate earns a focused first conversation." }).waitFor();
  check((await authPage.getByText("Candidate evidence").count()) >= 1, "Mandate Lens omitted candidate evidence");
  check((await authPage.getByText("Runs on this server with no external AI or third-party upload").count()) >= 1, "Mandate Lens omitted its local-processing boundary");
  const lensFocus = await authPage.locator('[data-lens-result]').evaluate(element => ({
    focused: element === document.activeElement,
    hash: window.location.hash,
    activeElement: document.activeElement?.tagName || "none"
  }));
  check(lensFocus.focused, `Mandate Lens result did not receive focus after analysis (hash=${lensFocus.hash || "none"}, active=${lensFocus.activeElement})`);
  const lensMetrics = await authPage.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
    bodyScrollWidth: document.body.scrollWidth
  }));
  check(lensMetrics.scrollWidth <= lensMetrics.clientWidth, `Mandate Lens 320x568: root horizontal overflow ${lensMetrics.scrollWidth}/${lensMetrics.clientWidth}`);
  check(lensMetrics.bodyScrollWidth <= lensMetrics.clientWidth, `Mandate Lens 320x568: body horizontal overflow ${lensMetrics.bodyScrollWidth}/${lensMetrics.clientWidth}`);
  await authPage.locator('.skip-link').evaluate(element => { element.style.display = "none"; });
  for (const proofViewport of [
    { name: "mobile-390x844", width: 390, height: 844 },
    { name: "desktop-1440x900", width: 1440, height: 900 }
  ]) {
    await authPage.setViewportSize({ width: proofViewport.width, height: proofViewport.height });
    await authPage.locator('[data-lens-result]').focus();
    await authPage.screenshot({
      path: path.join(outputDir, `mandate-lens-review-${proofViewport.name}.png`),
      fullPage: true,
      animations: "disabled"
    });
  }
  await authPage.locator('textarea[name="MandateNote"]').fill("Synthetic QA context proving explicit encrypted sharing.");
  await authPage.getByRole("button", { name: "Share this brief privately" }).click();
  await authPage.getByText("Your Mandate Lens brief was encrypted and shared privately with René.").waitFor();

  await authPage.locator('input[name="MessageSubject"]').fill("QA opportunity context");
  await authPage.locator('textarea[name="MessageBody"]').fill("This is a browser acceptance message proving the encrypted inbound workflow and redirect.");
  await authPage.getByRole("button", { name: "Send private message" }).click();
  await authPage.getByText("Your message has been saved for René.").waitFor();

  await authPage.locator('input[name="ConfirmDeletion"][type="checkbox"]').check();
  await authPage.getByRole("button", { name: "Delete my account" }).click();
  await authPage.waitForURL(`${baseUrl}/`);
  const afterDeletion = await authPage.goto(`${baseUrl}/portal`, { waitUntil: "networkidle" });
  check(afterDeletion?.url().includes("/auth/login"), "deleted account retained an authenticated portal session");
}
await authContext.close();

await request.close();
await browser.close();
await fs.writeFile(path.join(outputDir, "portal-qa-report.json"), `${JSON.stringify({ results, failures }, null, 2)}\n`, "utf8");

if (failures.length > 0) {
  console.error(`Portal QA failed with ${failures.length} issue(s):`);
  failures.forEach(failure => console.error(`- ${failure}`));
  process.exit(1);
}
console.log(`Portal QA passed across ${viewports.length} viewports plus Chromium and WebKit touch contexts.`);
