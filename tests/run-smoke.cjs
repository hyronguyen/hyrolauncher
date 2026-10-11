'use strict';
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const tests = [
  'verify-dialogs.cjs',
  'verify-mp3-player.cjs',
  'verify-pet-motion.cjs',
  'verify-ui-effects.cjs',
  'verify-hybrowser-scrollbar.cjs'
];
for (const test of tests) {
  process.stdout.write(`\n${test}\n`);
  execFileSync(process.execPath, [path.join(__dirname, test)], { stdio: 'inherit' });
}
console.log(`\nPASS: ${tests.length} offline smoke suites.`);
