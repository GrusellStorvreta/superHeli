const assert = require('assert');
const { greet } = require('../src/index.js');

assert.strictEqual(greet('Tester'), 'Hello, Tester!');
console.log('Node example test passed');
