import fs from "node:fs/promises";
import path from "node:path";
import { chromium } from "playwright";
import AxeBuilder from "@axe-core/playwright";

const baseUrl = (process.env.QA_BASE_URL || "http://web:8080").replace(/\/$/, "");
const outputDir = process.env.QA_OUTPUT_DIR || "/work/artifacts/local";

const viewports = [
  { name: "320x568", width: 320, height: 568 },
  { name: "390x844", width: 390, height: 844 },
  { name: "768x1024", width: 768, height: 1024 },
  { name: "1024x768", width: 1024, height: 768 },
  { name: "1440x900", width: 1440, height: 900 },
  { name: "1920x1080", width: 1920, height: 1080 }
];

const failures = [];
const results = [];

function check(condition, message) {
  if (!condition) failures.push(message);
}

await fs.mkdir(outputDir, { recursive: true });
const browser = await chromium.launch({ headless: true });

for (const viewport of viewports) {
  const context = await browser.newContext({
    viewport: { width: viewport.width, height: viewport.height },
    colorScheme: "light",
    reducedMotion: "no-preference"
  });
  const page = await context.newPage();
  const consoleErrors = [];
  const failedRequests = [];

  page.on("console", message => {
    if (message.type() === "error") consoleErrors.push(message.text());
  });
  page.on("requestfailed", request => {
    failedRequests.push(`${request.url()}: ${request.failure()?.errorText || "failed"}`);
  });

  const response = await page.goto(`${baseUrl}/`, { waitUntil: "networkidle" });
  check(response?.status() === 200, `${viewport.name}: homepage returned ${response?.status()}`);
  await page.evaluate(() => document.fonts?.ready);

  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
    h1Count: document.querySelectorAll("h1").length,
    title: document.title,
    lang: document.documentElement.lang,
    canonical: document.querySelector('link[rel="canonical"]')?.href,
    linkedIn: [...document.querySelectorAll("a")].filter(link => link.textContent.includes("LinkedIn")).map(link => link.href),
    xLinks: [...document.querySelectorAll("a")].filter(link => link.href.includes("x.com/")).map(link => link.href),
    headings: [...document.querySelectorAll("h1,h2,h3")].map(heading => Number(heading.tagName.slice(1))),
    targets: [...document.querySelectorAll(".brand-mark,.header-cta,.button,.text-link,.site-footer a")]
      .filter(element => {
        const style = getComputedStyle(element);
        const rect = element.getBoundingClientRect();
        return style.visibility !== "hidden" && style.display !== "none" && rect.width > 0 && rect.height > 0;
      })
      .map(element => ({ label: element.textContent.trim(), ...element.getBoundingClientRect().toJSON() }))
  }));

  check(metrics.scrollWidth <= metrics.clientWidth + 1, `${viewport.name}: horizontal overflow ${metrics.scrollWidth}/${metrics.clientWidth}`);
  check(metrics.h1Count === 1, `${viewport.name}: expected one h1, found ${metrics.h1Count}`);
  check(metrics.lang === "en-AU", `${viewport.name}: lang is ${metrics.lang}`);
  check(metrics.canonical === "https://reneb.au/", `${viewport.name}: canonical is ${metrics.canonical}`);
  check(metrics.linkedIn.length >= 1 && metrics.linkedIn.every(url => url === "https://www.linkedin.com/in/renebrauwers/"), `${viewport.name}: LinkedIn URL mismatch`);
  check(metrics.xLinks.length >= 1 && metrics.xLinks.every(url => url === "https://x.com/Rene_B"), `${viewport.name}: X URL mismatch`);
  check(metrics.headings.every((level, index, levels) => index === 0 || level <= levels[index - 1] + 1), `${viewport.name}: heading level skipped`);
  for (const target of metrics.targets) {
    check(target.width >= 44 && target.height >= 44, `${viewport.name}: target '${target.label}' is ${Math.round(target.width)}x${Math.round(target.height)}`);
  }

  check(consoleErrors.length === 0, `${viewport.name}: console errors: ${consoleErrors.join(" | ")}`);
  check(failedRequests.length === 0, `${viewport.name}: failed requests: ${failedRequests.join(" | ")}`);

  const axe = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"]).analyze();
  const seriousAxe = axe.violations.filter(violation => ["serious", "critical"].includes(violation.impact));
  check(
    seriousAxe.length === 0,
    `${viewport.name}: axe serious/critical: ${seriousAxe.map(violation => `${violation.id} [${violation.nodes.map(node => node.target.join(" ")).join(", ")}]`).join("; ")}`
  );

  await page.screenshot({ path: path.join(outputDir, `${viewport.name}.png`), fullPage: true });
  results.push({ viewport: viewport.name, axeViolations: axe.violations.length, consoleErrors: consoleErrors.length, failedRequests: failedRequests.length });
  await context.close();
}

{
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, javaScriptEnabled: false });
  const page = await context.newPage();
  const response = await page.goto(`${baseUrl}/`, { waitUntil: "load" });
  check(response?.status() === 200, `JavaScript-disabled homepage returned ${response?.status()}`);
  check(await page.locator("main").isVisible(), "JavaScript-disabled main content is not visible");
  check(await page.locator("a", { hasText: "Connect on LinkedIn" }).first().isVisible(), "JavaScript-disabled primary CTA is not visible");
  await page.screenshot({ path: path.join(outputDir, "javascript-disabled-390x844.png"), fullPage: true });
  await context.close();
}

{
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, reducedMotion: "reduce" });
  const page = await context.newPage();
  await page.goto(`${baseUrl}/`);
  const scrollBehavior = await page.evaluate(() => getComputedStyle(document.documentElement).scrollBehavior);
  check(scrollBehavior === "auto", `Reduced-motion scroll behavior is ${scrollBehavior}`);
  await context.close();
}

{
  const context = await browser.newContext({ viewport: { width: 720, height: 450 }, deviceScaleFactor: 2 });
  const page = await context.newPage();
  await page.goto(`${baseUrl}/`);
  const width = await page.evaluate(() => ({ scroll: document.documentElement.scrollWidth, client: document.documentElement.clientWidth }));
  check(width.scroll <= width.client + 1, `200% zoom equivalent: horizontal overflow ${width.scroll}/${width.client}`);
  await page.screenshot({ path: path.join(outputDir, "zoom-200-percent.png"), fullPage: true });
  await context.close();
}

{
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, colorScheme: "dark" });
  const page = await context.newPage();
  await page.goto(`${baseUrl}/`);
  await page.screenshot({ path: path.join(outputDir, "dark-os-390x844.png"), fullPage: true });
  await context.close();
}

{
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, forcedColors: "active" });
  const page = await context.newPage();
  await page.goto(`${baseUrl}/`);
  check(await page.locator("h1").isVisible(), "Forced-colour mode h1 is not visible");
  await page.screenshot({ path: path.join(outputDir, "forced-colours-390x844.png"), fullPage: true });
  await context.close();
}

{
  const context = await browser.newContext({ viewport: { width: 1024, height: 768 } });
  const page = await context.newPage();
  await page.goto(`${baseUrl}/`);
  await page.keyboard.press("Tab");
  check(await page.locator(".skip-link").evaluate(element => element === document.activeElement), "Skip link is not the first keyboard target");
  await page.keyboard.press("Enter");
  await page.waitForTimeout(100);
  check(await page.evaluate(() => location.hash === "#main-content"), "Skip link did not navigate to main content");
  check(await page.locator("#main-content").evaluate(element => element === document.activeElement), "Skip link did not focus main content");
  await context.close();
}

{
  const request = await browser.newPage();
  const assetPaths = ["/styles.css", "/favicon.svg", "/social-card.png", "/robots.txt", "/sitemap.xml"];
  for (const assetPath of assetPaths) {
    const response = await request.request.get(`${baseUrl}${assetPath}`);
    check(response.status() === 200, `${assetPath} returned ${response.status()}`);
  }

  const socialPage = await browser.newPage({ viewport: { width: 1200, height: 630 } });
  await socialPage.setContent(`<img src="${baseUrl}/social-card.png" alt="">`);
  const socialSize = await socialPage.locator("img").evaluate(image => ({ width: image.naturalWidth, height: image.naturalHeight }));
  check(socialSize.width === 1200 && socialSize.height === 630, `Social card is ${socialSize.width}x${socialSize.height}`);
  await socialPage.close();

  const missing = await request.request.get(`${baseUrl}/not-a-real-page`, { maxRedirects: 0 });
  check(missing.status() === 404, `Unknown path returned ${missing.status()}`);
  const index = await request.request.get(`${baseUrl}/index.html`, { maxRedirects: 0 });
  check([301, 308].includes(index.status()), `/index.html returned ${index.status()}`);

  const home = await request.request.get(`${baseUrl}/`);
  const headers = home.headers();
  check(Boolean(headers["content-security-policy"]), "Content-Security-Policy header missing");
  check(headers["x-content-type-options"] === "nosniff", "X-Content-Type-Options header missing or incorrect");
  check(Boolean(headers["permissions-policy"]), "Permissions-Policy header missing");
  check(headers["referrer-policy"] === "strict-origin-when-cross-origin", "Referrer-Policy header missing or incorrect");
  await request.close();
}

await browser.close();

const report = { baseUrl, viewports: results, failures };
await fs.writeFile(path.join(outputDir, "qa-report.json"), `${JSON.stringify(report, null, 2)}\n`, "utf8");

if (failures.length > 0) {
  console.error(`QA failed with ${failures.length} issue(s):`);
  for (const failure of failures) console.error(`- ${failure}`);
  process.exit(1);
}

console.log(`QA passed for ${baseUrl} across ${viewports.length} required viewports.`);
