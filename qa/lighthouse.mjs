import fs from "node:fs/promises";
import path from "node:path";
import { spawn } from "node:child_process";
import lighthouse from "lighthouse";
import { chromium } from "playwright";

const baseUrl = (process.env.QA_BASE_URL || "http://web:8080").replace(/\/$/, "");
const outputDir = process.env.QA_OUTPUT_DIR || "/work/artifacts/local";

await fs.mkdir(outputDir, { recursive: true });

const port = 9222;
const chrome = spawn(chromium.executablePath(), [
  "--headless=new",
  "--no-sandbox",
  "--disable-dev-shm-usage",
  `--remote-debugging-port=${port}`,
  "--user-data-dir=/tmp/lighthouse-chrome",
  "about:blank"
], {
  env: { ...process.env, HOME: "/tmp" },
  stdio: ["ignore", "pipe", "pipe"]
});

const chromeStderr = [];
chrome.stderr.on("data", chunk => chromeStderr.push(chunk.toString()));

let chromeReady = false;
for (let attempt = 0; attempt < 100; attempt += 1) {
  if (chrome.exitCode !== null) break;
  try {
    const response = await fetch(`http://127.0.0.1:${port}/json/version`);
    if (response.ok) {
      chromeReady = true;
      break;
    }
  } catch {
    // Chrome is still starting.
  }
  await new Promise(resolve => setTimeout(resolve, 100));
}

if (!chromeReady) {
  throw new Error(`Chromium did not expose its debugging port. ${chromeStderr.join("")}`);
}

try {
  const run = await lighthouse(`${baseUrl}/`, {
    port,
    output: "json",
    logLevel: "error",
    onlyCategories: ["performance", "accessibility", "best-practices", "seo"]
  });

  const lhr = run.lhr;
  const scores = Object.fromEntries(Object.entries(lhr.categories).map(([key, category]) => [key, Math.round(category.score * 100)]));
  const metrics = {
    lcpMs: Math.round(lhr.audits["largest-contentful-paint"].numericValue),
    cls: lhr.audits["cumulative-layout-shift"].numericValue,
    tbtMs: Math.round(lhr.audits["total-blocking-time"].numericValue)
  };

  await fs.writeFile(path.join(outputDir, "lighthouse-report.json"), run.report, "utf8");
  await fs.writeFile(path.join(outputDir, "lighthouse-summary.json"), `${JSON.stringify({ baseUrl, scores, metrics }, null, 2)}\n`, "utf8");

  const thresholds = {
    performance: 95,
    accessibility: 95,
    "best-practices": 95,
    seo: 95
  };
  const failed = Object.entries(thresholds).filter(([key, minimum]) => scores[key] < minimum);
  if (metrics.lcpMs >= 2500) failed.push(["lcpMs", 2500]);
  if (metrics.cls >= 0.1) failed.push(["cls", 0.1]);

  console.log(JSON.stringify({ scores, metrics }, null, 2));
  if (failed.length > 0) {
    console.error(`Lighthouse thresholds failed: ${failed.map(([key]) => key).join(", ")}`);
    process.exitCode = 1;
  }
} finally {
  chrome.kill("SIGTERM");
}
