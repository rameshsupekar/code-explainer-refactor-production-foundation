import { After, Given, IWorldOptions, Then, When, World, setWorldConstructor } from '@cucumber/cucumber';
import * as assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';
import { spawnSync } from 'node:child_process';
import { AnalysisRequest, AnalysisResponse } from './worker-models';

class AnalyzerWorld extends World {
  rootPath: string;
  request: AnalysisRequest = { rootPath: '', files: [], frameworks: ['typescript', 'react', 'angular'] };
  response?: AnalysisResponse;
  outsideFile?: string;

  constructor(options: IWorldOptions) {
    super(options);
    this.rootPath = fs.mkdtempSync(path.join(os.tmpdir(), 'code-explainer-worker-bdd-'));
  }
}

setWorldConstructor(AnalyzerWorld);

Given('a TypeScript worker request containing a source file with a service class', function (this: AnalyzerWorld) {
  writeSource(this, 'src/catalog.ts', "export class CatalogService { getCatalog(): string { return 'catalog'; } }");
});

Given('a TypeScript worker request containing a function that calls a literal API route', function (this: AnalyzerWorld) {
  writeSource(this, 'src/client.ts', "export function loadCatalog() { return fetch('/api/catalog'); }");
});

Given('a TypeScript worker request containing a fetch call with a dynamic route', function (this: AnalyzerWorld) {
  writeSource(this, 'src/client.ts', 'export function load(route: string) { return fetch(route); }');
});

Given('a TypeScript worker request referencing a source file outside its root', function (this: AnalyzerWorld) {
  this.outsideFile = `${this.rootPath}-outside.ts`;
  fs.writeFileSync(this.outsideFile, 'export const outside = true;');
  this.request = {
    rootPath: this.rootPath,
    files: [`../${path.basename(this.outsideFile)}`],
    frameworks: ['typescript'],
  };
});

When('the worker analyzes the request', function (this: AnalyzerWorld) {
  const result = spawnSync(process.execPath, [path.join(__dirname, 'index.js')], {
    cwd: __dirname,
    encoding: 'utf8',
    input: `${JSON.stringify(this.request)}\n`,
  });
  assert.ifError(result.error);
  assert.ok(result.stdout, result.stderr || 'Worker returned no response');
  this.response = JSON.parse(result.stdout) as AnalysisResponse;
});

Then('the response contains the source module and service class symbols', function (this: AnalyzerWorld) {
  assert.equal(this.response?.success, true);
  const names = this.response?.symbols.map(symbol => symbol.name) ?? [];
  assert.ok(names.includes('catalog'));
  assert.ok(names.includes('CatalogService'));
});

Then('the response contains an HTTP endpoint symbol and an owning call relationship', function (this: AnalyzerWorld) {
  assert.equal(this.response?.success, true);
  const endpoint = this.response?.symbols.find(symbol => symbol.name === 'GET /api/catalog');
  assert.ok(endpoint);
  assert.ok(this.response?.relationships.some(relationship =>
    relationship.kind === 'Calls' && relationship.targetId === endpoint.id));
});

Then('the response contains a warning for the unresolved HTTP route', function (this: AnalyzerWorld) {
  assert.ok(this.response?.diagnostics?.some(diagnostic => diagnostic.code === 'TSHTTP001' && diagnostic.severity === 'Warning'));
});

Then('the response reports that the file is outside the analysis root', function (this: AnalyzerWorld) {
  assert.equal(this.response?.success, false);
  assert.match(this.response?.error ?? '', /outside the analysis root/i);
});

After(function (this: AnalyzerWorld) {
  fs.rmSync(this.rootPath, { recursive: true, force: true });
  if (this.outsideFile)
    fs.rmSync(this.outsideFile, { force: true });
});

function writeSource(world: AnalyzerWorld, relativePath: string, source: string): void {
  const sourcePath = path.join(world.rootPath, relativePath);
  fs.mkdirSync(path.dirname(sourcePath), { recursive: true });
  fs.writeFileSync(sourcePath, source);
  world.request = {
    rootPath: world.rootPath,
    files: [relativePath],
    frameworks: ['typescript', 'react', 'angular'],
  };
}
