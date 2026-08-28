#!/usr/bin/env node
// Generates manifest.json for the data/ folder: relative path, SHA-256 content hash,
// and public download URL for every file (recursively). Used by deploy-data-release.yml.
import { createHash } from 'node:crypto';
import { readdir, readFile, writeFile } from 'node:fs/promises';
import { join, relative, sep } from 'node:path';

const dataDir = process.argv[2] ?? 'data';
const outputFile = process.argv[3] ?? 'manifest.json';
const baseUrl = process.env.DATA_R2_BASE_URL;

if (!baseUrl) {
  console.error('DATA_R2_BASE_URL is not set.');
  process.exit(1);
}

const normalizedBase = baseUrl.replace(/\/+$/, '');

async function walk(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const fullPath = join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await walk(fullPath)));
    } else if (entry.isFile()) {
      files.push(fullPath);
    }
  }
  return files;
}

function toUrl(relativePath) {
  return `${normalizedBase}/${relativePath
    .split(sep)
    .map(encodeURIComponent)
    .join('/')}`;
}

try {
  const filePaths = await walk(dataDir);
  const entries = [];
  for (const fullPath of filePaths.sort()) {
    const bytes = await readFile(fullPath);
    const relativePath = relative(dataDir, fullPath).split(sep).join('/');
    entries.push({
      path: relativePath,
      name: relativePath.substring(relativePath.lastIndexOf('/') + 1),
      hash: createHash('sha256').update(bytes).digest('hex'),
      url: toUrl(relativePath),
    });
  }

  const manifest = {
    version: 1,
    generatedAtUtc: new Date().toISOString(),
    fileCount: entries.length,
    files: entries,
  };

  await writeFile(outputFile, JSON.stringify(manifest, null, 2) + '\n', 'utf8');
  console.log(`Generated ${outputFile} with ${entries.length} file(s) from ${dataDir}/`);
} catch (error) {
  console.error('Failed to generate data manifest:', error);
  process.exit(1);
}
