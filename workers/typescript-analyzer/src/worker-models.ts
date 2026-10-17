export interface AnalysisRequest {
  rootPath: string;
  files: string[];
  frameworks: string[];
}

export interface AnalysisResponse {
  success: boolean;
  symbols: CodeSymbolDto[];
  relationships: CodeRelationshipDto[];
  diagnostics?: AnalysisDiagnosticDto[];
  error?: string;
}

export interface AnalysisDiagnosticDto {
  code: string;
  severity: 'Info' | 'Warning' | 'Error';
  message: string;
  filePath?: string;
  location?: LocationDto;
}

export interface CodeSymbolDto {
  id: string;
  name: string;
  kind: SymbolKindDto;
  language: string;
  filePath: string;
  location: LocationDto;
  members: CodeMemberDto[];
  metadata: Record<string, string>;
}

export interface CodeMemberDto {
  id: string;
  name: string;
  kind: MemberKindDto;
  location: LocationDto;
  parameters: string[];
  returnType?: string;
}

export interface LocationDto {
  startLine: number;
  endLine: number;
  startColumn: number;
  endColumn: number;
}

export interface CodeRelationshipDto {
  sourceId: string;
  targetId: string;
  kind: RelationshipKindDto;
  label?: string;
}

export type SymbolKindDto =
  | 'Namespace'
  | 'Module'
  | 'Class'
  | 'Interface'
  | 'Method'
  | 'Function'
  | 'Component'
  | 'Service'
  | 'Directive'
  | 'Pipe'
  | 'Controller'
  | 'Endpoint'
  | 'Hook'
  | 'ContextProvider'
  | 'Type'
  | 'Enum'
  | 'Decorator';

export type MemberKindDto =
  | 'Method'
  | 'Property'
  | 'Field'
  | 'Constructor'
  | 'Getter'
  | 'Setter'
  | 'Parameter';

export type RelationshipKindDto =
  | 'Contains'
  | 'Imports'
  | 'Inherits'
  | 'Implements'
  | 'Calls'
  | 'Injects'
  | 'Uses'
  | 'Renders'
  | 'RoutesTo'
  | 'Reads'
  | 'Writes'
  | 'Decorates'
  | 'Provides'
  | 'References';