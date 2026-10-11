import test from 'node:test';
import assert from 'node:assert/strict';
import { measureImage } from '../../../extensions/chromium/acquire.js';
import { ownershipCases, installDom } from '../../../tests/browser/ownership-fixtures.mjs';
for (const name of ownershipCases) test(`actual old measure must reject ${name}`, () => {
 const fixture = installDom(name, 'https://public-fixture.invalid/image.png');
 try { assert.equal(measureImage('https://public-fixture.invalid/image.png', 'ffffffff-ffff-ffff-ffff-ffffffffffff'), null); }
 finally { fixture.restore(); }
});
