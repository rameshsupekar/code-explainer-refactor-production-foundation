import * as fs from 'node:fs';
import * as path from 'node:path';
import * as readline from 'node:readline';
import * as ts from 'typescript';
import {
  AnalysisRequest,
  AnalysisResponse,
  AnalysisDiagnosticDto,
  CodeMemberDto,
  CodeRelationshipDto,
  CodeSymbolDto,
  LocationDto,
} from './worker-models';

type Framework = 'typescript' | 'react' | 'angular';

interface SourceContext {
  absolutePath: string;
  relativePath: string;
  source: ts.SourceFile;
  module: CodeSymbolDto;
  declarations: Map<ts.Node, CodeSymbolDto>;
  imports: Array<{ specifier: string; target?: string }>;
}

const responseFor = (request: AnalysisRequest): AnalysisResponse => {
  const rootPath = path.resolve(request.rootPath);
  const frameworks = new Set(request.frameworks.map(value => value.toLowerCase() as Framework));
  const sources: SourceContext[] = [];
  const templates = new Map<string, string>();
  const diagnostics: AnalysisDiagnosticDto[] = [];

  for (const requestedFile of request.files) {
    const absolutePath = path.resolve(rootPath, requestedFile);
    const relativePath = path.relative(rootPath, absolutePath);
    if (relativePath === '..' || relativePath.startsWith(`..${path.sep}`) || path.isAbsolute(relativePath)) {
      throw new Error(`File is outside the analysis root: ${requestedFile}`);
    }

    const extension = path.extname(absolutePath).toLowerCase();
    if (extension === '.html') {
      templates.set(absolutePath, fs.readFileSync(absolutePath, 'utf8'));
      continue;
    }
    if (extension !== '.ts' && extension !== '.tsx' && extension !== '.js' && extension !== '.jsx') continue;

    const text = fs.readFileSync(absolutePath, 'utf8');
    const relative = relativePath.split(path.sep).join('/');
    const source = ts.createSourceFile(
      absolutePath,
      text,
      ts.ScriptTarget.Latest,
      true,
      extension === '.tsx' ? ts.ScriptKind.TSX : extension === '.jsx' ? ts.ScriptKind.JSX :
        extension === '.js' ? ts.ScriptKind.JS : ts.ScriptKind.TS,
    );
    const parseDiagnostics = (source as ts.SourceFile & { parseDiagnostics: readonly ts.Diagnostic[] }).parseDiagnostics;
    diagnostics.push(...parseDiagnostics.map(diagnostic => ({
      code: `TS${diagnostic.code}`,
      severity: diagnostic.category === ts.DiagnosticCategory.Error ? 'Error' as const : 'Warning' as const,
      message: ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n'),
      filePath: absolutePath,
      location: diagnostic.start === undefined ? undefined : locationAt(source, diagnostic.start, diagnostic.length),
    })));
    const module = createSymbol(
      `typescript:${relative}:<module>`,
      path.basename(absolutePath, extension),
      'Module',
      relative,
      locationOf(source, source),
      [],
      { relativePath: relative },
    );
    sources.push({ absolutePath, relativePath: relative, source, module, declarations: new Map(), imports: [] });
  }

  const symbols: CodeSymbolDto[] = [];
  const relationships: CodeRelationshipDto[] = [];
  const symbolByName = new Map<string, CodeSymbolDto[]>();
  for (const context of sources) {
    symbols.push(context.module);
    const addSymbol = (symbol: CodeSymbolDto): void => {
      symbols.push(symbol);
      const matches = symbolByName.get(symbol.name) ?? [];
      matches.push(symbol);
      symbolByName.set(symbol.name, matches);
    };

    for (const statement of context.source.statements) {
      const symbol = analyzeDeclaration(statement, context, frameworks);
      if (symbol) {
        context.declarations.set(statement, symbol);
        addSymbol(symbol);
        relationships.push({ sourceId: context.module.id, targetId: symbol.id, kind: 'Contains' });
      }

      if (ts.isImportDeclaration(statement) && ts.isStringLiteral(statement.moduleSpecifier)) {
        context.imports.push({ specifier: statement.moduleSpecifier.text });
      }
    }
  }

  const moduleIds = new Map(sources.map(context => [context.absolutePath, context.module.id]));
  for (const context of sources) {
    for (const imported of context.imports) {
      const targetPath = resolveImport(context.absolutePath, imported.specifier, sources);
      const targetId = targetPath ? moduleIds.get(targetPath) : undefined;
      if (targetId) relationships.push({
        sourceId: context.module.id,
        targetId,
        kind: 'Imports',
        label: imported.specifier,
      });
    }

    for (const symbol of context.declarations.values()) {
      const dependencies = (symbol.metadata.dependencies ?? '').split(',').filter(Boolean);
      for (const dependency of dependencies) {
        const match = symbolByName.get(dependency)?.find(candidate => candidate.id !== symbol.id);
        if (match) relationships.push({ sourceId: symbol.id, targetId: match.id, kind: frameworks.has('angular') ? 'Injects' : 'Uses' });
      }
    }
  }

  for (const context of sources) {
    for (const [node, symbol] of context.declarations) {
      if (symbol.kind === 'Component' && frameworks.has('react')) {
        addReactRenderRelationships(node, symbol, context.source, symbolByName, relationships);
      }
      if (symbol.kind === 'Component' && frameworks.has('angular')) {
        addAngularTemplateRelationships(symbol, context.absolutePath, templates, symbolByName, relationships);
      }
      addInheritanceRelationships(node, symbol, symbolByName, relationships);
    }
    if (frameworks.has('angular')) addAngularRouteRelationships(context, symbolByName, relationships);
  }

  for (const context of sources) {
    addHttpRequestSymbols(context, symbols, relationships, diagnostics);
  }

  return { success: true, symbols, relationships: uniqueRelationships(relationships), diagnostics };
};

const analyzeDeclaration = (
  node: ts.Statement,
  context: SourceContext,
  frameworks: Set<Framework>,
): CodeSymbolDto | undefined => {
  if (ts.isClassDeclaration(node) && node.name) {
    const decorators = decoratorNames(node);
    const reactComponent = frameworks.has('react') &&
      (extendsReactComponent(node) || node.members.some(member => ts.isMethodDeclaration(member) &&
        member.name.getText(context.source) === 'render' && containsJsx(member)));
    const kind = angularKind(decorators) ?? (reactComponent ? 'Component' : 'Class');
    const metadata: Record<string, string> = {};
    if (decorators.length) metadata.decorators = decorators.join(',');
    if (kind === 'Component' && decorators.includes('Component')) {
      const values = decoratorObject(node);
      if (values.selector) metadata.selector = values.selector;
      if (values.templateUrl) metadata.templateUrl = values.templateUrl;
      if (values.template) metadata.template = values.template;
    }
    if (kind === 'Service') {
      const values = decoratorObject(node);
      if (values.providedIn) metadata.providedIn = values.providedIn;
    }
    const heritage = node.heritageClauses?.flatMap(clause => clause.types.map(type => type.expression.getText(context.source))) ?? [];
    if (heritage.length) metadata.baseTypes = heritage.join(',');
    const constructor = node.members.find(ts.isConstructorDeclaration);
    const dependencies = constructor?.parameters.map(parameter => parameter.type?.getText(context.source) ?? '')
      .filter(Boolean) ?? [];
    if (dependencies.length) metadata.dependencies = [...new Set(dependencies)].join(',');
    return createSymbol(
      symbolId(context, node.name.text), node.name.text, kind, context.relativePath,
      locationOf(context.source, node), node.members.map(member => memberDto(context.source, member)).filter(isDefined), metadata,
    );
  }

  if (ts.isInterfaceDeclaration(node) || ts.isTypeAliasDeclaration(node) || ts.isEnumDeclaration(node)) {
    const name = node.name.text;
    const kind = ts.isInterfaceDeclaration(node) ? 'Interface' : ts.isEnumDeclaration(node) ? 'Enum' : 'Type';
    const members = 'members' in node && Array.isArray(node.members)
      ? node.members.map(member => memberDto(context.source, member)).filter(isDefined)
      : [];
    return createSymbol(symbolId(context, name), name, kind, context.relativePath, locationOf(context.source, node), members, {});
  }

  if (ts.isFunctionDeclaration(node) && node.name) {
    const isHook = frameworks.has('react') && /^use[A-Z0-9]/.test(node.name.text);
    const isComponent = frameworks.has('react') && /^[A-Z]/.test(node.name.text) && containsJsx(node);
    const kind = isHook ? 'Hook' : isComponent ? 'Component' : 'Function';
    return createSymbol(
      symbolId(context, node.name.text), node.name.text, kind, context.relativePath,
      locationOf(context.source, node), [], {},
    );
  }

  if (ts.isVariableStatement(node)) {
    const results: CodeSymbolDto[] = [];
    for (const declaration of node.declarationList.declarations) {
      if (!ts.isIdentifier(declaration.name) || !declaration.initializer) continue;
      const name = declaration.name.text;
      const initializer = declaration.initializer;
      const isHook = frameworks.has('react') && /^use[A-Z0-9]/.test(name);
      const isComponent = frameworks.has('react') && /^[A-Z]/.test(name) && containsJsx(initializer);
      const isContext = frameworks.has('react') && ts.isCallExpression(initializer) &&
        initializer.expression.getText(context.source).endsWith('createContext');
      if (!ts.isArrowFunction(initializer) && !ts.isFunctionExpression(initializer) && !isContext) continue;
      const kind = isHook ? 'Hook' : isComponent ? 'Component' : isContext ? 'ContextProvider' : 'Function';
      results.push(createSymbol(
        symbolId(context, name), name, kind, context.relativePath,
        locationOf(context.source, declaration), [], {},
      ));
    }
    if (results.length) {
      context.declarations.set(node, results[0]);
      return results[0];
    }
  }

  return undefined;
};

const memberDto = (source: ts.SourceFile, member: ts.ClassElement | ts.TypeElement): CodeMemberDto | undefined => {
  if (ts.isConstructorDeclaration(member)) {
    return {
      id: `${source.fileName}:${member.pos}:constructor`, name: 'constructor', kind: 'Constructor',
      location: locationOf(source, member), parameters: member.parameters.map(parameter => parameter.getText(source)),
    };
  }
  if (ts.isMethodDeclaration(member) || ts.isMethodSignature(member)) {
    const name = member.name.getText(source);
    return {
      id: `${source.fileName}:${member.pos}:${name}`, name, kind: 'Method', location: locationOf(source, member),
      parameters: member.parameters.map(parameter => parameter.getText(source)),
      returnType: member.type?.getText(source),
    };
  }
  if (ts.isPropertyDeclaration(member) || ts.isPropertySignature(member)) {
    const name = member.name.getText(source);
    return {
      id: `${source.fileName}:${member.pos}:${name}`, name, kind: 'Property', location: locationOf(source, member),
      parameters: [], returnType: member.type?.getText(source),
    };
  }
  return undefined;
};

const addHttpRequestSymbols = (
  context: SourceContext,
  symbols: CodeSymbolDto[],
  relationships: CodeRelationshipDto[],
  diagnostics: AnalysisDiagnosticDto[],
): void => {
  const visit = (node: ts.Node): void => {
    if (ts.isCallExpression(node)) {
      const details = httpRequestDetails(node, context.source);
      if (details) {
        if (!details.route) {
          diagnostics.push({
            code: 'TSHTTP001',
            severity: 'Warning',
            message: 'Dynamic HTTP request route was not linked to a backend endpoint.',
            filePath: context.absolutePath,
            location: locationOf(context.source, node),
          });
        } else {
          const request = createSymbol(
            `typescript:${context.relativePath}:request:${node.getStart(context.source)}`,
            `${details.method} /${details.route}`,
            'Endpoint',
            context.relativePath,
            locationOf(context.source, node),
            [],
            { httpMethod: details.method, route: details.route, requestStyle: 'http-client' },
          );
          const owner = [...context.declarations.entries()]
            .filter(([declaration]) => declaration.pos <= node.pos && declaration.end >= node.end)
            .sort((left, right) => (left[0].end - left[0].pos) - (right[0].end - right[0].pos))[0]?.[1]
            ?? context.module;
          symbols.push(request);
          relationships.push({
            sourceId: owner.id,
            targetId: request.id,
            kind: 'Calls',
            label: `${details.method} /${details.route}`,
          });
        }
      }
    }
    node.forEachChild(visit);
  };
  visit(context.source);
};

const httpRequestDetails = (
  call: ts.CallExpression,
  source: ts.SourceFile,
): { method: string; route?: string } | undefined => {
  const expression = call.expression;
  let method: string | undefined;
  let routeExpression: ts.Expression | undefined;
  let optionsExpression: ts.Expression | undefined;

  if (ts.isIdentifier(expression) && expression.text === 'fetch') {
    method = 'GET';
    routeExpression = call.arguments[0];
    optionsExpression = call.arguments[1];
  } else if (ts.isPropertyAccessExpression(expression)) {
    const candidate = expression.name.text.toUpperCase();
    if (['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'].includes(candidate)) {
      const receiver = expression.expression.getText(source).toLowerCase();
      routeExpression = call.arguments[0];
      const literalRoute = routeExpression && ts.isStringLiteralLike(routeExpression)
        ? routeExpression.text
        : undefined;
      if (/(axios|http|client|api)/.test(receiver) || literalRoute?.startsWith('/') || literalRoute?.startsWith('http'))
        method = candidate;
    }
  }

  if (!method) return undefined;
  if (optionsExpression && ts.isObjectLiteralExpression(optionsExpression)) {
    const methodProperty = optionsExpression.properties.find(property =>
      ts.isPropertyAssignment(property) && property.name.getText(source).replaceAll(/["']/g, '') === 'method');
    if (methodProperty && ts.isPropertyAssignment(methodProperty) && ts.isStringLiteralLike(methodProperty.initializer))
      method = methodProperty.initializer.text.toUpperCase();
  }

  if (!routeExpression || !ts.isStringLiteralLike(routeExpression)) return { method };
  const route = routeExpression.text
    .replace(/^https?:\/\/[^/]+/i, '')
    .split(/[?#]/, 1)[0]
    .replace(/^\/+/, '')
    .replace(/\/+$/, '');
  return { method, route };
};

const addReactRenderRelationships = (
  node: ts.Node,
  sourceSymbol: CodeSymbolDto,
  source: ts.SourceFile,
  symbols: Map<string, CodeSymbolDto[]>,
  relationships: CodeRelationshipDto[],
): void => {
  const rendered = new Set<string>();
  const visit = (child: ts.Node): void => {
    if (ts.isJsxOpeningElement(child) || ts.isJsxSelfClosingElement(child)) {
      if (ts.isIdentifier(child.tagName)) {
        for (const target of symbols.get(child.tagName.text) ?? []) {
          if (target.kind === 'Component' && target.id !== sourceSymbol.id) rendered.add(target.id);
        }
      }
    }
    child.forEachChild(visit);
  };
  node.forEachChild(visit);
  for (const targetId of rendered) relationships.push({ sourceId: sourceSymbol.id, targetId, kind: 'Renders' });
};

const addAngularTemplateRelationships = (
  component: CodeSymbolDto,
  sourcePath: string,
  templates: Map<string, string>,
  symbols: Map<string, CodeSymbolDto[]>,
  relationships: CodeRelationshipDto[],
): void => {
  const inlineTemplate = component.metadata.template;
  const templatePath = component.metadata.templateUrl
    ? path.resolve(path.dirname(sourcePath), component.metadata.templateUrl)
    : undefined;
  const text = inlineTemplate ?? (templatePath ? templates.get(templatePath) : undefined);
  if (!text) return;
  for (const [name, candidates] of symbols) {
    for (const target of candidates) {
      const selector = target.metadata.selector;
      if (target.kind === 'Component' && selector && selectorMatches(text, selector) && target.id !== component.id) {
        relationships.push({ sourceId: component.id, targetId: target.id, kind: 'Renders' });
      }
    }
  }
};

const addInheritanceRelationships = (
  node: ts.Node,
  symbol: CodeSymbolDto,
  symbols: Map<string, CodeSymbolDto[]>,
  relationships: CodeRelationshipDto[],
): void => {
  if (!ts.isClassDeclaration(node) && !ts.isInterfaceDeclaration(node)) return;
  for (const clause of node.heritageClauses ?? []) {
    for (const type of clause.types) {
      const name = type.expression.getText().split('.').pop() ?? '';
      const target = symbols.get(name)?.find(candidate => candidate.id !== symbol.id);
      if (target) relationships.push({
        sourceId: symbol.id,
        targetId: target.id,
        kind: clause.token === ts.SyntaxKind.ExtendsKeyword ? 'Inherits' : 'Implements',
      });
    }
  }
};

const addAngularRouteRelationships = (
  context: SourceContext,
  symbols: Map<string, CodeSymbolDto[]>,
  relationships: CodeRelationshipDto[],
): void => {
  const visit = (node: ts.Node): void => {
    if (ts.isObjectLiteralExpression(node)) {
      const component = node.properties.find(property => ts.isPropertyAssignment(property) && property.name.getText(context.source) === 'component');
      const route = node.properties.find(property => ts.isPropertyAssignment(property) && property.name.getText(context.source) === 'path');
      if (component && ts.isPropertyAssignment(component) && ts.isIdentifier(component.initializer)) {
        const target = symbols.get(component.initializer.text)?.find(symbol => symbol.kind === 'Component');
        const routePath = route && ts.isPropertyAssignment(route) && ts.isStringLiteral(route.initializer)
          ? route.initializer.text
          : undefined;
        if (target) relationships.push({ sourceId: context.module.id, targetId: target.id, kind: 'RoutesTo', label: routePath });
      }
    }
    node.forEachChild(visit);
  };
  visit(context.source);
};

const decoratorNames = (node: ts.Node): string[] =>
  (ts.canHaveDecorators(node) ? ts.getDecorators(node) ?? [] : []).map(decorator => {
    const expression = ts.isCallExpression(decorator.expression) ? decorator.expression.expression : decorator.expression;
    return expression.getText().split('.').pop() ?? '';
  });

const angularKind = (decorators: string[]): CodeSymbolDto['kind'] | undefined => {
  if (decorators.includes('Component')) return 'Component';
  if (decorators.includes('Injectable')) return 'Service';
  if (decorators.includes('Directive')) return 'Directive';
  if (decorators.includes('Pipe')) return 'Pipe';
  if (decorators.includes('NgModule')) return 'Module';
  return undefined;
};

const decoratorObject = (node: ts.Node): Record<string, string> => {
  const decorator = ts.canHaveDecorators(node) ? ts.getDecorators(node)?.find(item =>
    ts.isCallExpression(item.expression) && ts.isObjectLiteralExpression(item.expression.arguments[0])) : undefined;
  if (!decorator || !ts.isCallExpression(decorator.expression) || !ts.isObjectLiteralExpression(decorator.expression.arguments[0])) return {};
  const values: Record<string, string> = {};
  for (const property of decorator.expression.arguments[0].properties) {
    if (!ts.isPropertyAssignment(property) || !ts.isIdentifier(property.name) && !ts.isStringLiteral(property.name)) continue;
    const key = property.name.text;
    if (ts.isStringLiteral(property.initializer) || ts.isNoSubstitutionTemplateLiteral(property.initializer)) {
      values[key] = property.initializer.text;
    }
  }
  return values;
};

const extendsReactComponent = (node: ts.ClassDeclaration): boolean =>
  node.heritageClauses?.some(clause => clause.token === ts.SyntaxKind.ExtendsKeyword &&
    clause.types.some(type => /(?:React\.)?(?:PureComponent|Component)/.test(type.expression.getText()))) ?? false;

const containsJsx = (node: ts.Node): boolean => {
  let found = false;
  const visit = (child: ts.Node): void => {
    if (ts.isJsxElement(child) || ts.isJsxSelfClosingElement(child)) found = true;
    if (!found) child.forEachChild(visit);
  };
  visit(node);
  return found;
};

const resolveImport = (sourcePath: string, specifier: string, sources: SourceContext[]): string | undefined => {
  if (!specifier.startsWith('.')) return undefined;
  const candidate = path.resolve(path.dirname(sourcePath), specifier);
  const available = new Set(sources.map(source => source.absolutePath));
  const attempts = [candidate, `${candidate}.ts`, `${candidate}.tsx`, `${candidate}.js`,
    path.join(candidate, 'index.ts'), path.join(candidate, 'index.tsx')];
  return attempts.find(attempt => available.has(attempt));
};

const symbolId = (context: SourceContext, name: string): string =>
  `typescript:${context.relativePath}:${name}`;

const createSymbol = (
  id: string,
  name: string,
  kind: CodeSymbolDto['kind'],
  filePath: string,
  location: LocationDto,
  members: CodeMemberDto[],
  metadata: Record<string, string>,
): CodeSymbolDto => ({ id, name, kind, language: 'typescript', filePath, location, members, metadata });

const locationOf = (source: ts.SourceFile, node: ts.Node): LocationDto => {
  return locationAt(source, node.getStart(source), node.end - node.getStart(source));
};

const locationAt = (source: ts.SourceFile, position: number, length = 0): LocationDto => {
  const start = source.getLineAndCharacterOfPosition(position);
  const end = source.getLineAndCharacterOfPosition(position + length);
  return {
    startLine: start.line + 1,
    endLine: end.line + 1,
    startColumn: start.character + 1,
    endColumn: end.character + 1,
  };
};

const selectorMatches = (template: string, selector: string): boolean => {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  return new RegExp(`(?:<|\\s)${escaped}(?:\\s|>|/)`, 'i').test(template);
};

const uniqueRelationships = (relationships: CodeRelationshipDto[]): CodeRelationshipDto[] => {
  const seen = new Set<string>();
  return relationships.filter(relationship => {
    const key = `${relationship.sourceId}\u0000${relationship.targetId}\u0000${relationship.kind}\u0000${relationship.label ?? ''}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
};

const isDefined = <T>(value: T | undefined): value is T => value !== undefined;

const input = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
let requestLine = '';
input.on('line', line => {
  if (!requestLine) requestLine = line;
});
input.on('close', () => {
  try {
    const request = JSON.parse(requestLine) as AnalysisRequest;
    const response = responseFor(request);
    process.stdout.write(`${JSON.stringify(response)}\n`);
  } catch (error) {
    const response: AnalysisResponse = {
      success: false,
      symbols: [],
      relationships: [],
      diagnostics: [],
      error: error instanceof Error ? error.message : String(error),
    };
    process.stdout.write(`${JSON.stringify(response)}\n`);
    process.exitCode = 1;
  }
});
