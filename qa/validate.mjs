import fs from "node:fs/promises";
import path from "node:path";
import { chromium, devices, webkit } from "playwright";
import AxeBuilder from "@axe-core/playwright";

const baseUrl = (process.env.QA_BASE_URL || "http://web:8080").replace(/\/$/, "");
const outputDir = process.env.QA_OUTPUT_DIR || "/work/artifacts/local";
const analyticsUrl = "https://stats.reneb.au/script.js";
const analyticsWebsiteId = "55c627ba-826f-4472-9479-f1279071488c";

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
const touchResults = [];

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

  const heroArtwork = page.locator(".hero-portrait img");
  check(await heroArtwork.getAttribute("loading") !== "lazy", `${viewport.name}: hero artwork is lazy-loaded`);
  check(await heroArtwork.getAttribute("fetchpriority") === "high", `${viewport.name}: hero artwork fetch priority is not high`);
  await page.waitForFunction(() => {
    const image = document.querySelector(".hero-portrait img");
    return image?.complete && image.naturalWidth > 0;
  });
  const heroImage = await heroArtwork.evaluate(image => ({
    naturalWidth: image.naturalWidth,
    naturalHeight: image.naturalHeight,
    alt: image.alt
  }));
  check(heroImage.naturalWidth === 1200 && heroImage.naturalHeight === 900, `${viewport.name}: hero artwork is ${heroImage.naturalWidth}x${heroImage.naturalHeight}`);
  check(heroImage.alt.length > 0, `${viewport.name}: hero artwork alt text is empty`);
  await heroArtwork.evaluate(image => image.decode());
  await page.waitForTimeout(100);
  const captureOverlays = page.locator(".site-header, .skip-link");
  await captureOverlays.evaluateAll(elements => elements.forEach(element => {
    element.hidden = true;
  }));
  await page.locator(".ai-section").screenshot({
    path: path.join(outputDir, `ai-section-${viewport.name}.png`),
    animations: "disabled"
  });
  await captureOverlays.evaluateAll(elements => elements.forEach(element => {
    element.hidden = false;
  }));

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
    bodyFontSize: Number.parseFloat(getComputedStyle(document.body).fontSize),
    heroIntroFontSize: Number.parseFloat(getComputedStyle(document.querySelector(".hero-intro")).fontSize),
    eyebrowFontSize: Number.parseFloat(getComputedStyle(document.querySelector(".eyebrow")).fontSize),
    wordmark: document.querySelector(".brand-copy strong")?.textContent.trim(),
    bodyText: document.body.innerText,
    analyticsScripts: [...document.querySelectorAll('script[src^="https://stats.reneb.au/"]')].map(script => ({
      src: script.src,
      websiteId: script.dataset.websiteId,
      domains: script.dataset.domains,
      defer: script.defer
    })),
    targets: [...document.querySelectorAll(".brand-home,.header-link,.header-cta,.button,.text-link,.site-footer a")]
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
  check(metrics.wordmark === "René Brauwers", `${viewport.name}: wordmark is '${metrics.wordmark}'`);
  check(metrics.bodyText.includes("Head of Enterprise Architecture"), `${viewport.name}: current role is missing`);
  check(metrics.bodyText.includes("Perpetual Corporate Trust"), `${viewport.name}: employer is missing`);
  check(metrics.bodyText.includes("Yes, this website was coded by AI."), `${viewport.name}: AI transparency statement is missing`);
  check(metrics.bodyText.includes("Cookieless, self-hosted analytics measure aggregate visits and approximate city/region."), `${viewport.name}: analytics privacy notice is missing`);
  check(metrics.analyticsScripts.length === 1, `${viewport.name}: expected one approved analytics script, found ${metrics.analyticsScripts.length}`);
  check(metrics.analyticsScripts[0]?.src === analyticsUrl, `${viewport.name}: analytics script URL is ${metrics.analyticsScripts[0]?.src}`);
  check(metrics.analyticsScripts[0]?.websiteId === analyticsWebsiteId, `${viewport.name}: analytics website ID is ${metrics.analyticsScripts[0]?.websiteId}`);
  check(metrics.analyticsScripts[0]?.domains === "reneb.au", `${viewport.name}: analytics domain restriction is ${metrics.analyticsScripts[0]?.domains}`);
  check(metrics.analyticsScripts[0]?.defer === true, `${viewport.name}: analytics script is not deferred`);
  check(metrics.bodyFontSize >= 18, `${viewport.name}: body text is ${metrics.bodyFontSize}px`);
  check(metrics.heroIntroFontSize >= 18, `${viewport.name}: hero introduction is ${metrics.heroIntroFontSize}px`);
  check(metrics.eyebrowFontSize >= 13, `${viewport.name}: eyebrow text is ${metrics.eyebrowFontSize}px`);
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

  await page.evaluate(() => scrollTo(0, 0));
  await page.waitForTimeout(50);
  await page.locator(".skip-link").evaluate(element => {
    element.hidden = true;
  });
  await page.screenshot({ path: path.join(outputDir, `${viewport.name}.png`), fullPage: true });
  await page.locator(".skip-link").evaluate(element => {
    element.hidden = false;
  });
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
  await page.waitForFunction(() => {
    const image = document.querySelector(".hero-portrait img");
    return image?.complete && image.naturalWidth > 0;
  });
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
  const assetPaths = ["/styles.css", "/favicon.svg", "/social-card.png", "/assets/human-ai-collaboration.webp", "/robots.txt", "/sitemap.xml"];
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
  check(index.headers().location === "/", `/index.html redirected to ${index.headers().location || "no location"} instead of /`);

  const home = await request.request.get(`${baseUrl}/`);
  const headers = home.headers();
  const contentSecurityPolicy = headers["content-security-policy"] || "";
  check(Boolean(contentSecurityPolicy), "Content-Security-Policy header missing");
  const scriptDirective = contentSecurityPolicy.split(";").find(value => value.trim().startsWith("script-src")) || "";
  const connectDirective = contentSecurityPolicy.split(";").find(value => value.trim().startsWith("connect-src")) || "";
  check(scriptDirective.includes("'self'") && scriptDirective.includes("https://stats.reneb.au"), "CSP does not allow the approved analytics script origin");
  check(connectDirective.includes("'self'") && connectDirective.includes("https://stats.reneb.au"), "CSP does not allow analytics collection at the approved origin");
  check(headers["x-content-type-options"] === "nosniff", "X-Content-Type-Options header missing or incorrect");
  check(Boolean(headers["permissions-policy"]), "Permissions-Policy header missing");
  check(headers["referrer-policy"] === "strict-origin-when-cross-origin", "Referrer-Policy header missing or incorrect");
  await request.close();
}

for (const touchBrowserName of ["chromium", "webkit"]) {
  const touchBrowser = touchBrowserName === "chromium" ? browser : await webkit.launch({ headless: true });
  const context = await touchBrowser.newContext({
    ...devices["iPhone 13"],
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
  check(response?.status() === 200, `${touchBrowserName} touch: homepage returned ${response?.status()}`);

  const touchMetrics = await page.evaluate(() => {
    const portrait = document.querySelector(".hero-portrait").getBoundingClientRect();
    const brand = document.querySelector(".brand-home").getBoundingClientRect();
    return {
      clientWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
      bodyScrollWidth: document.body.scrollWidth,
      rootOverflowX: getComputedStyle(document.documentElement).overflowX,
      bodyOverflowX: getComputedStyle(document.body).overflowX,
      rootOverscrollX: getComputedStyle(document.documentElement).overscrollBehaviorX,
      bodyOverscrollX: getComputedStyle(document.body).overscrollBehaviorX,
      portraitLeft: portrait.left,
      portraitRight: portrait.right,
      portraitWidth: portrait.width,
      brandWidth: brand.width,
      brandHeight: brand.height,
      h1FontSize: Number.parseFloat(getComputedStyle(document.querySelector("h1")).fontSize),
      heroIntroFontSize: Number.parseFloat(getComputedStyle(document.querySelector(".hero-intro")).fontSize)
    };
  });

  const horizontalPosition = await page.evaluate(() => {
    document.documentElement.scrollLeft = 10000;
    document.body.scrollLeft = 10000;
    return {
      windowX: window.scrollX,
      rootX: document.documentElement.scrollLeft,
      bodyX: document.body.scrollLeft
    };
  });

  check(touchMetrics.scrollWidth <= touchMetrics.clientWidth, `${touchBrowserName} touch: root overflow ${touchMetrics.scrollWidth}/${touchMetrics.clientWidth}`);
  check(touchMetrics.bodyScrollWidth <= touchMetrics.clientWidth, `${touchBrowserName} touch: body overflow ${touchMetrics.bodyScrollWidth}/${touchMetrics.clientWidth}`);
  check(["hidden", "clip"].includes(touchMetrics.rootOverflowX), `${touchBrowserName} touch: root overflow-x is ${touchMetrics.rootOverflowX}`);
  check(["hidden", "clip"].includes(touchMetrics.bodyOverflowX), `${touchBrowserName} touch: body overflow-x is ${touchMetrics.bodyOverflowX}`);
  check(touchMetrics.rootOverscrollX === "none" && touchMetrics.bodyOverscrollX === "none", `${touchBrowserName} touch: horizontal overscroll is ${touchMetrics.rootOverscrollX}/${touchMetrics.bodyOverscrollX}`);
  check(horizontalPosition.windowX === 0 && horizontalPosition.rootX === 0 && horizontalPosition.bodyX === 0, `${touchBrowserName} touch: horizontal position moved ${JSON.stringify(horizontalPosition)}`);
  check(touchMetrics.portraitLeft >= 0 && touchMetrics.portraitRight <= touchMetrics.clientWidth, `${touchBrowserName} touch: portrait escapes viewport ${touchMetrics.portraitLeft}/${touchMetrics.portraitRight}`);
  check(touchMetrics.portraitWidth <= touchMetrics.clientWidth - 40, `${touchBrowserName} touch: portrait is ${Math.round(touchMetrics.portraitWidth)}px in a ${touchMetrics.clientWidth}px viewport`);
  check(touchMetrics.brandWidth >= 44 && touchMetrics.brandHeight >= 44, `${touchBrowserName} touch: wordmark target is ${Math.round(touchMetrics.brandWidth)}x${Math.round(touchMetrics.brandHeight)}`);
  check(touchMetrics.h1FontSize >= 40, `${touchBrowserName} touch: hero heading is ${touchMetrics.h1FontSize}px`);
  check(touchMetrics.heroIntroFontSize >= 18, `${touchBrowserName} touch: hero introduction is ${touchMetrics.heroIntroFontSize}px`);

  if (touchBrowserName === "chromium") {
    const captureOverlays = page.locator(".site-header, .skip-link");
    await captureOverlays.evaluateAll(elements => elements.forEach(element => {
      element.hidden = true;
    }));
    await page.locator(".hero-portrait").screenshot({
      path: path.join(outputDir, `touch-${touchBrowserName}-hero-390x844.png`)
    });
    await captureOverlays.evaluateAll(elements => elements.forEach(element => {
      element.hidden = false;
    }));
  }
  await page.waitForTimeout(100);

  check(consoleErrors.length === 0, `${touchBrowserName} touch: console errors: ${consoleErrors.join(" | ")}`);
  check(failedRequests.length === 0, `${touchBrowserName} touch: failed requests: ${failedRequests.join(" | ")}`);

  touchResults.push({ browser: touchBrowserName, ...touchMetrics, horizontalPosition, consoleErrors, failedRequests });
  await context.close();
  if (touchBrowserName === "webkit") await touchBrowser.close();
}

await browser.close();

const report = { baseUrl, viewports: results, touch: touchResults, failures };
await fs.writeFile(path.join(outputDir, "qa-report.json"), `${JSON.stringify(report, null, 2)}\n`, "utf8");

if (failures.length > 0) {
  console.error(`QA failed with ${failures.length} issue(s):`);
  for (const failure of failures) console.error(`- ${failure}`);
  process.exit(1);
}

console.log(`QA passed for ${baseUrl} across ${viewports.length} required viewports.`);
