import fs from "node:fs/promises";
import { HtmlValidate } from "html-validate";

const htmlPath = "/work/site/index.html";
const html = await fs.readFile(htmlPath, "utf8");
const htmlvalidate = new HtmlValidate({
  extends: ["html-validate:recommended"],
  rules: {
    "long-title": "off",
    "no-inline-style": "error",
    "wcag/h30": "error",
    "wcag/h37": "error",
    "wcag/h63": "error"
  }
});
const report = await htmlvalidate.validateString(html, htmlPath);

if (!report.valid) {
  for (const result of report.results) {
    for (const message of result.messages) {
      console.error(`${result.filePath}:${message.line}:${message.column} ${message.ruleId} ${message.message}`);
    }
  }
  process.exit(1);
}

console.log("HTML validation passed.");
