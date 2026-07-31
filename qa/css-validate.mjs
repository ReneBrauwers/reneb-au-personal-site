import fs from "node:fs/promises";
import postcss from "postcss";

const cssPath = "/work/site/styles.css";
const css = await fs.readFile(cssPath, "utf8");
postcss.parse(css, { from: cssPath });
console.log("CSS parsing passed.");
