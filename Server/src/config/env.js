const fs = require('fs');
const path = require('path');

const SERVER_ROOT = path.resolve(__dirname, '..', '..');
const ENV_FILES = ['.env', '.env.local'];
const originalEnvKeys = new Set(Object.keys(process.env));

function stripOuterQuotes(value) {
  if (value.length < 2) return value;

  const first = value[0];
  const last = value[value.length - 1];
  if ((first === '"' && last === '"') || (first === "'" && last === "'")) {
    return value.slice(1, -1);
  }
  return value;
}

function loadEnvFile(filePath) {
  if (!fs.existsSync(filePath)) return;

  const contents = fs.readFileSync(filePath, 'utf8');
  for (const line of contents.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;

    const delimiterIndex = line.indexOf('=');
    if (delimiterIndex <= 0) continue;

    const key = line.slice(0, delimiterIndex).trim();
    if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(key)) continue;
    if (originalEnvKeys.has(key)) continue;

    const value = stripOuterQuotes(line.slice(delimiterIndex + 1).trim());
    process.env[key] = value;
  }
}

function loadEnv() {
  for (const fileName of ENV_FILES) {
    loadEnvFile(path.join(SERVER_ROOT, fileName));
  }
}

loadEnv();

module.exports = { loadEnv };
