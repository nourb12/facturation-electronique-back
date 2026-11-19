import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { readFileSync, readdirSync, statSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join, resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const backendRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const option = (name, fallback) => {
  const index = args.indexOf(name);
  if (index === -1) return fallback;
  if (!args[index + 1] || args[index + 1].startsWith('--')) throw new Error(`Missing value for ${name}`);
  return args[index + 1];
};
const frontendRoot = resolve(option('--frontend-root', join(backendRoot, '../../frontendpfe/ey-invoice-portal')));
const buildId = option('--build-id', `local-${new Date().toISOString().replace(/[^0-9TZ]/g, '')}`);
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const git = (root, ...parameters) => execFileSync('git', ['-c', `safe.directory=${root}`, '-C', root, ...parameters], { encoding: 'utf8' }).trim();
const excluded = file => file.split(/[\\/]/).some(part =>
  ['bin', 'obj', 'node_modules', 'uploads', '.docker', '.angular', 'coverage', 'TestResults'].includes(part)
  || part === '.env' || part.startsWith('.env.') || /appsettings\.(Development|Local)\.json/i.test(part));

function fingerprint(root, files) {
  const hash = createHash('sha256');
  for (const file of [...files].sort()) hash.update(file.replaceAll('\\', '/')).update('\0').update(readFileSync(join(root, file))).update('\0');
  return hash.digest('hex');
}

function source(root, paths) {
  const files = git(root, 'ls-files', '-z', '--cached', '--others', '--exclude-standard', '--', ...paths)
    .split('\0').filter(Boolean).filter(file => !excluded(file) && statSafe(join(root, file)));
  if (files.length === 0) throw new Error(`No source files found in ${root}`);
  return {
    commit: git(root, 'rev-parse', 'HEAD'),
    dirty: git(root, 'status', '--porcelain', '--untracked-files=normal', '--', ...paths).length > 0,
    fingerprint: fingerprint(root, files),
    fileCount: files.length
  };
}

function statSafe(path) {
  try { return statSync(path).isFile(); } catch (error) { if (error.code === 'ENOENT') return false; throw error; }
}

function tree(root, directory = root) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const path = join(directory, entry.name);
    if (entry.isSymbolicLink()) throw new Error(`Runtime artifacts must not contain symlinks: ${path}`);
    if (entry.isDirectory()) return tree(root, path);
    return entry.name === 'build-manifest.json' ? [] : [relative(root, path)];
  });
}

const components = [
  { name: 'backend', root: backendRoot, output: join(backendRoot, '.docker/backend-publish'), entry: 'Einvoicing.Api.dll',
    paths: ['Einvoicing.Api', 'Einvoicing.Application', 'Einvoicing.Domain', 'Einvoicing.Infrastructure', 'Dockerfile.local', 'Jenkinsfile', 'devops/runtime-manifest.mjs'] },
  { name: 'frontend', root: frontendRoot, output: join(frontendRoot, 'dist/tuniflow'), entry: 'index.html',
    paths: ['src', 'public', 'templates', 'package.json', 'package-lock.json', 'angular.json', 'tsconfig.json', 'tsconfig.app.json', 'tailwind.config.js', 'postcss.config.js', 'Dockerfile.local', 'nginx.conf'] }
];
const createdAt = new Date().toISOString();
const manifests = components.map(component => {
  const bytes = readFileSync(join(component.output, component.entry));
  if (bytes.length === 0) throw new Error(`Empty entry point: ${component.entry}`);
  return { schemaVersion: 1, buildId, createdAt, component: component.name,
    source: source(component.root, component.paths),
    artifact: { entryPoint: component.entry, sha256: sha256(bytes), fingerprint: fingerprint(component.output, tree(component.output)) }
  };
});
for (const [index, manifest] of manifests.entries()) {
  writeFileSync(join(components[index].output, 'build-manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
}
const evidenceRoot = join(backendRoot, '.docker/provenance');
mkdirSync(evidenceRoot, { recursive: true });
writeFileSync(join(evidenceRoot, 'runtime-manifests.json'), JSON.stringify(manifests, null, 2) + '\n');
console.log(`Runtime manifests written for build ${buildId}; source changes are explicitly marked.`);
