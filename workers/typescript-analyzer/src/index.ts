import * as ts from 'typescript';
import * as fs from 'fs';
import * as path from 'path';
import { AnalysisRequest, AnalysisResponse, CodeMemberDto, CodeRelationshipDto, CodeSymbolDto, LocationDto } from './models';

class TypeScriptAnalyzer {
  private checker!: ts.TypeChecker;
  private frameworks: string[] = [];
  private symbols: CodeSymbolDto[] = [];
  private relationships: CodeRelationshipDto[] = [];

  analyze(request: AnalysisRequest): AnalysisResponse {
    this.frameworks = request.frameworks ?? [];
    this.symbols = [];
    this.relationships = [];
    const root = path.resolve(request.rootPath);
    const requested = request.files.map(file => path.resolve(root, file));
    const configPath = ts.findConfigFile(root, ts.sys.fileExists, 'tsconfig.json');
    let options: ts.CompilerOptions = { target: ts.ScriptTarget.ES2020, module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, experimentalDecorators: true };
    let fileNames = requested;
    if (configPath) {
      const config = ts.readConfigFile(configPath, ts.sys.readFile);
      if (config.error) throw new Error(ts.flattenDiagnosticMessageText(config.error.messageText, '\n'));
      const parsed = ts.parseJsonConfigFileContent(config.config, ts.sys, path.dirname(configPath));
      options = { ...parsed.options, ...options };
      if (fileNames.length === 0) fileNames = parsed.fileNames;
    }
    this.program = ts.createProgram(fileNames, options);
    this.checker = this.program.getTypeChecker();
    for (const source of this.program.getSourceFiles()) {
      if (!source.isDeclarationFile && source.fileName.startsWith(root)) this.extractFile(source);
    }
    return { success: true, symbols: this.symbols, relationships: this.relationships };
  }

  private program!: ts.Program;
  private extractFile(source: ts.SourceFile): void {
    const relative = path.relative(process.cwd(), source.fileName).replaceAll('\\', '/');
    const moduleId = `typescript:${relative}`;
    this.symbols.push({ id: moduleId, name: path.basename(source.fileName), kind: 'Module', language: 'typescript', filePath: source.fileName, location: this.location(source, source), members: [], metadata: {} });
    const visit = (node: ts.Node, owner?: CodeSymbolDto) => {
      if (ts.isImportDeclaration(node) && ts.isStringLiteral(node.moduleSpecifier)) {
        this.relationships.push({ sourceId: moduleId, targetId: `typescript:${node.moduleSpecifier.text}`, kind: 'Imports', label: node.moduleSpecifier.text });
      }
      let current = owner;
      if (ts.isClassDeclaration(node) && node.name) current = this.addType(node, node.name.text, this.isAngular(node) ? 'Component' : 'Class', source, owner);
      else if (ts.isInterfaceDeclaration(node)) current = this.addType(node, node.name.text, 'Interface', source, owner);
      else if (ts.isTypeAliasDeclaration(node)) current = this.addType(node, node.name.text, 'Type', source, owner);
      else if (ts.isEnumDeclaration(node)) current = this.addType(node, node.name.text, 'Enum', source, owner);
      else if (ts.isFunctionDeclaration(node) && node.name) this.addFunction(node, node.name.text, source, owner);
      else if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && this.isComponent(node)) this.addFunction(node, node.name.text, source, owner, true);
      ts.forEachChild(node, child => visit(child, current));
    };
    ts.forEachChild(source, node => visit(node));
  }

  private addType(node: ts.Node, name: string, kind: CodeSymbolDto['kind'], source: ts.SourceFile, owner?: CodeSymbolDto): CodeSymbolDto {
    const id = `typescript:${source.fileName}:${name}`;
    const symbol: CodeSymbolDto = { id, name, kind, language: this.frameworks.join(',') || 'typescript', filePath: source.fileName, location: this.location(node, source), members: [], metadata: {} };
    this.symbols.push(symbol); if (owner) this.relationships.push({ sourceId: owner.id, targetId: id, kind: 'Contains' });
    if (ts.isClassDeclaration(node) && node.heritageClauses) for (const h of node.heritageClauses) for (const t of h.types) this.relationships.push({ sourceId: id, targetId: `typescript:${t.expression.getText()}`, kind: h.token === ts.SyntaxKind.ImplementsKeyword ? 'Implements' : 'Inherits' });
    return symbol;
  }

  private addFunction(node: ts.FunctionDeclaration | ts.VariableDeclaration, name: string, source: ts.SourceFile, owner?: CodeSymbolDto, component = false): void {
    const kind: CodeSymbolDto['kind'] = component ? 'Component' : (name.startsWith('use') ? 'Hook' : 'Function');
    const id = `typescript:${source.fileName}:${name}`;
    const symbol: CodeSymbolDto = { id, name, kind, language: this.frameworks.join(',') || 'typescript', filePath: source.fileName, location: this.location(node, source), members: [], metadata: {} };
    this.symbols.push(symbol); if (owner) this.relationships.push({ sourceId: owner.id, targetId: id, kind: 'Contains' });
  }

  private isAngular(node: ts.ClassDeclaration): boolean { return this.frameworks.includes('angular') && node.modifiers?.some(m => m.getText().includes('Component')) === true; }
  private isComponent(node: ts.VariableDeclaration): boolean { return this.frameworks.includes('react') && /^[A-Z]/.test(node.name.getText()) && node.initializer?.getText().includes('=>') === true; }
  private location(node: ts.Node, source: ts.SourceFile): LocationDto { const a = source.getLineAndCharacterOfPosition(node.getStart(source)); const b = source.getLineAndCharacterOfPosition(node.getEnd()); return { startLine: a.line + 1, endLine: b.line + 1, startColumn: a.character + 1, endColumn: b.character + 1 }; }
}

const analyzer = new TypeScriptAnalyzer();
let buffer = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => {
  buffer += chunk;
  let index: number;
  while ((index = buffer.indexOf('\n')) >= 0) {
    const line = buffer.slice(0, index).trim(); buffer = buffer.slice(index + 1);
    if (!line) continue;
    try { const response: AnalysisResponse = analyzer.analyze(JSON.parse(line)); process.stdout.write(JSON.stringify(response) + '\n'); }
    catch (error) { process.stdout.write(JSON.stringify({ success: false, symbols: [], relationships: [], error: String(error) }) + '\n'); }
  }
});
