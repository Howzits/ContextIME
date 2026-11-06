'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const vm = require('node:vm');

function harness(initial = {}) {
  const settings = { enabled: true, otherInputs: 'chinese', pollInterval: 250, ...initial };
  const children = [];
  const commands = new Map();
  const timers = new Map();
  const status = { show() {}, dispose() {} };
  const output = { lines: [], appendLine(s) { this.lines.push(s); }, append(s) { this.lines.push(s); }, show() { this.shown = true; }, dispose() {} };
  let changed;
  const vscode = {
    StatusBarAlignment: { Right: 1 }, ConfigurationTarget: { Global: 1 },
    window: { createOutputChannel: () => output, createStatusBarItem: () => status, setStatusBarMessage() {} },
    workspace: {
      getConfiguration: () => ({ get: (key, fallback) => settings[key] ?? fallback, update: async (key, value) => {
        settings[key] = value; changed({ affectsConfiguration: () => true });
      } }),
      onDidChangeConfiguration: handler => { changed = handler; return { dispose() {} }; }
    },
    commands: { registerCommand: (name, fn) => { commands.set(name, fn); return { dispose() {} }; } }
  };
  const spawn = (file, args) => {
    const worker = new EventEmitter();
    worker.stdout = new EventEmitter(); worker.stderr = new EventEmitter(); worker.stdin = new EventEmitter();
    worker.stdin.end = () => { worker.ended = true; };
    worker.stdin.writable = true;
    worker.messages = [];
    worker.stdin.write = text => worker.messages.push(text);
    worker.kill = () => { worker.killed = true; };
    worker.args = args;
    children.push(worker);
    return worker;
  };
  const sandbox = {
    module: { exports: {} }, process: { platform: 'win32' },
    require: name => name === 'vscode' ? vscode : name === 'child_process' ? { spawn } : require(name),
    setTimeout: fn => { const id = {}; timers.set(id, fn); return id; },
    clearTimeout: id => timers.delete(id)
  };
  vm.runInNewContext(readFileSync(join(__dirname, '..', 'extension.js'), 'utf8'), sandbox);
  const context = { subscriptions: [], asAbsolutePath: name => join(__dirname, '..', name) };
  sandbox.module.exports.activate(context);
  return { settings, children, commands, timers, status, output,
    change() { changed({ affectsConfiguration: () => true }); },
    dispose() { sandbox.module.exports.deactivate(); },
    runRetry() { const [id, fn] = timers.entries().next().value; timers.delete(id); fn(); }
  };
}

test('settings reach the helper and configuration changes replace it', () => {
  const h = harness({ otherInputs: 'preserve', pollInterval: 400 });
  assert.deepEqual(Array.from(h.children[0].args), ['--other-inputs=preserve', '--poll=400', '--remember=true']);
  h.change();
  assert.equal(h.children[0].killed, true);
  assert.equal(h.children[0].ended, true);
  assert.equal(h.children.length, 2);
  h.dispose();
  assert.equal(h.children[1].killed, true);
});

test('partial messages distinguish requested target from failure', () => {
  const h = harness();
  const worker = h.children[0];
  worker.stdout.emit('data', Buffer.from('target=zh layoutReq'));
  assert.match(h.status.text, /自动/);
  worker.stdout.emit('data', Buffer.from('uest=True\r\n'));
  assert.match(h.status.text, /目标 中/);
  worker.stdout.emit('data', Buffer.from('target=en layoutRequest=False\n'));
  assert.match(h.status.text, /请求失败/);
  worker.stdout.emit('data', Buffer.from('focus=preserve\n'));
  assert.match(h.status.text, /保持/);
  h.dispose();
});

test('pause stops the helper, clears pending retry and resume starts it', async () => {
  const h = harness();
  h.children[0].emit('exit', 1);
  assert.equal(h.timers.size, 1);
  await h.commands.get('autoInput.toggle')();
  assert.equal(h.timers.size, 0);
  assert.match(h.status.text, /已暂停/);
  await h.commands.get('autoInput.toggle')();
  assert.equal(h.children.length, 2);
  h.dispose();
});

test('unexpected exits retry twice, disposed workers cannot update the status', () => {
  const h = harness();
  h.children[0].emit('exit', 1); h.runRetry();
  h.children[1].emit('exit', 1); h.runRetry();
  h.children[2].emit('exit', 1);
  assert.equal(h.timers.size, 0);
  assert.equal(h.children.length, 3);
  h.dispose();
  const text = h.status.text;
  h.children[0].stdout.emit('data', Buffer.from('target=zh layoutRequest=True\n'));
  assert.equal(h.status.text, text);
});

test('disabled extension does not start a helper', () => {
  const h = harness({ enabled: false });
  assert.equal(h.children.length, 0);
  assert.match(h.status.text, /已暂停/);
  h.dispose();
});

test('diagnostic and preference commands use the helper control channel', () => {
  const h = harness();
  h.commands.get('autoInput.diagnose')();
  h.commands.get('autoInput.preferEnglish')();
  h.commands.get('autoInput.preferChinese')();
  h.commands.get('autoInput.preferKeep')();
  h.commands.get('autoInput.clearPreferences')();
  assert.deepEqual(h.children[0].messages, ['diagnose\n', 'pin-en\n', 'pin-zh\n', 'pin-keep\n', 'clear\n']);
  h.children[0].stdout.emit('data', Buffer.from('diagnostic reason=code-editor default=keep\n'));
  assert.equal(h.output.shown, true);
  h.dispose();
});

test('diagnostic command reports a paused worker instead of starting another', () => {
  const h = harness({ enabled: false });
  h.commands.get('autoInput.diagnose')();
  assert.equal(h.children.length, 0);
  assert.match(h.output.lines.join('\n'), /助手未运行/);
  h.dispose();
});

test('preference memory can be disabled through configuration', () => {
  const h = harness({ rememberPreferences: false });
  assert.ok(h.children[0].args.includes('--remember=false'));
  h.dispose();
});
