import { HtmlValidate } from "html-validate";

const baseUrl = (process.env.QA_BASE_URL || "http://web:8080").replace(/\/$/, "");
const htmlvalidate = new HtmlValidate({
  extends: ["html-validate:recommended"],
  rules: {
    "long-title": "off",
    // Razor Tag Helpers emit antiforgery inputs using valid XML-style void syntax,
    // while hand-authored HTML uses the omitted-end-tag style.
    "void-style": "off",
    // ASP.NET emits a same-name hidden false value after each checkbox so an
    // unchecked control still binds deterministically on the server.
    "form-dup-name": "off",
    "no-inline-style": "error",
    "wcag/h30": "error",
    "wcag/h37": "error",
    "wcag/h63": "error"
  }
});
let failed = false;
for (const route of ["/", "/recruiters", "/privacy", "/auth/register", "/auth/login"]) {
  const response = await fetch(`${baseUrl}${route}`);
  if (!response.ok) {
    console.error(`${route}: returned ${response.status}`);
    failed = true;
    continue;
  }
  const report = await htmlvalidate.validateString(await response.text(), route);
  if (!report.valid) {
    failed = true;
    for (const result of report.results) {
      for (const message of result.messages) {
        console.error(`${route}:${message.line}:${message.column} ${message.ruleId} ${message.message}`);
      }
    }
  }
}
if (failed) process.exit(1);

console.log("Dynamic HTML validation passed for public and authentication pages.");
