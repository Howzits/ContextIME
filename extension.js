'use strict';
const vscode = require('vscode');
const { spawn } = require('child_process');
let stopActive = () => {};

function activate(context) {
  const output = vscode.window.createOutputChannel('ContextIME');
  const status = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 10);
  status.name = 'ContextIME';
  status.command = 'autoInput.toggle';
  status.tooltip = 'ContextIME：终端英文 · 对话中文。点击暂停/启用。目标语言不代表已确认实际输入状态。';
  let child;
  let retry;
  let retries = 0;
  let disposed = false;
  const config = () => vscode.workspace.getConfiguration('autoInput');

  function stop() {
    clearTimeout(retry);
    retry = undefined;
    if (child) {
      const old = child;
      child = undefined;
      old.stdin.end();
      old.kill();
    }
  }

  function start(resetRetries = true) {
    stop();
    if (disposed) return;
    if (resetRetries) retries = 0;
    status.show();
    if (process.platform !== 'win32') {
      status.text = '$(info) ContextIME: Windows only';
      return;
    }
    if (!config().get('enabled', true)) {
      status.text = '$(debug-pause) ContextIME: 已暂停';
      return;
    }
    const args = [
      `--other-inputs=${config().get('otherInputs', 'chinese') === 'preserve' ? 'preserve' : 'chinese'}`,
      `--poll=${config().get('pollInterval', 250)}`,
      `--remember=${config().get('rememberPreferences', true)}`
    ];
    const worker = spawn(context.asAbsolutePath('bin/InputHelper.exe'), args, {
      windowsHide: true, stdio: ['pipe', 'pipe', 'pipe']
    });
    child = worker;
    status.text = '$(keyboard) ContextIME: 自动';
    output.appendLine(`${new Date().toISOString()} 启动焦点助手 ${args.join(' ')}`);
    let buffer = '';
    worker.stdout.on('data', data => {
      if (child !== worker) return;
      buffer += data.toString('utf8');
      const lines = buffer.split(/\r?\n/);
      buffer = lines.pop();
      for (const line of lines) {
        output.appendLine(line);
        const target = /target=(en|zh) layoutRequest=(True|False)/.exec(line);
        if (target) {
          status.text = target[2] === 'True'
            ? `$(keyboard) ContextIME: 目标 ${target[1] === 'en' ? 'EN' : '中'}`
            : '$(warning) ContextIME: 请求失败';
        } else if (line.includes('focus=preserve')) status.text = '$(keyboard) ContextIME: 保持';
        else if (line.includes('No configured input method')) status.text = '$(warning) ContextIME: 缺少输入法';
        else if (line.startsWith('diagnostic ')) output.show(true);
        else if (/^preference=(en|zh|keep) source=command/.test(line)) {
          vscode.window.setStatusBarMessage('ContextIME：已记住此输入框偏好（仅本次会话）。', 4000);
        } else if (line.startsWith('preferences=cleared')) {
          vscode.window.setStatusBarMessage('ContextIME：已清除本次会话的输入框偏好。', 4000);
        } else if (line.startsWith('preference=skipped') || line.startsWith('command=')) output.show(true);
      }
    });
    worker.stderr.on('data', data => output.append(data.toString()));
    worker.stdin.on('error', () => {});
    worker.on('error', error => {
      output.appendLine(`助手启动失败：${error.message}`);
      if (child === worker) status.text = '$(warning) ContextIME: 启动失败';
    });
    worker.on('exit', code => {
      if (child !== worker) return;
      child = undefined;
      status.text = '$(warning) ContextIME: 助手已停止';
      output.appendLine(`助手退出 (${code})`);
      if (!disposed && config().get('enabled', true) && retries < 2) {
        retries += 1;
        retry = setTimeout(() => start(false), 1000 * retries);
      }
    });
  }

  const shutdown = () => { disposed = true; stop(); };
  function send(command) {
    if (!child || !child.stdin.writable) {
      output.appendLine('助手未运行；请先启用 ContextIME 并重启助手。');
      output.show(true);
      return;
    }
    child.stdin.write(command + '\n');
    if (command !== 'clear') vscode.window.setStatusBarMessage('ContextIME：请在 3 秒内点击目标输入框。', 3000);
  }
  stopActive = shutdown;
  context.subscriptions.push(output, status,
    vscode.commands.registerCommand('autoInput.toggle', async () => {
      await config().update('enabled', !config().get('enabled', true), vscode.ConfigurationTarget.Global);
    }),
    vscode.commands.registerCommand('autoInput.showLog', () => output.show()),
    vscode.commands.registerCommand('autoInput.restart', () => start()),
    vscode.commands.registerCommand('autoInput.diagnose', () => send('diagnose')),
    vscode.commands.registerCommand('autoInput.preferEnglish', () => send('pin-en')),
    vscode.commands.registerCommand('autoInput.preferChinese', () => send('pin-zh')),
    vscode.commands.registerCommand('autoInput.preferKeep', () => send('pin-keep')),
    vscode.commands.registerCommand('autoInput.clearPreferences', () => send('clear')),
    vscode.workspace.onDidChangeConfiguration(event => {
      if (event.affectsConfiguration('autoInput')) start();
    }),
    { dispose: shutdown }
  );
  start();
}

function deactivate() { stopActive(); }
module.exports = { activate, deactivate };
