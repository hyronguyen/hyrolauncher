'use strict';
const fs = require('node:fs');
const path = require('node:path');
const projectRoot = path.resolve(__dirname, '..');
const verificationRoot = path.join(projectRoot, 'output', 'verification');
function prepare() {
  // Older assertions use project-relative source paths. Keep that contract even
  // when a verifier is launched from another working directory.
  process.chdir(projectRoot);
  fs.mkdirSync(verificationRoot, { recursive: true });
}
function playwright() {
  if (process.env.PLAYWRIGHT_MODULE) return require(process.env.PLAYWRIGHT_MODULE);
  try { return require('playwright'); }
  catch (error) {
    if (error.code !== 'MODULE_NOT_FOUND') throw error;
    throw new Error('Install test dependencies with npm install, or set PLAYWRIGHT_MODULE to an installed Playwright module.', { cause: error });
  }
}
module.exports = { projectRoot, verificationRoot, prepare, playwright };
