Feature: Analyze TypeScript source through the worker protocol
  As the CodeExplainer analysis pipeline
  I want the TypeScript worker to inspect supplied source files
  So that JavaScript ecosystem modules and application interactions appear in the model

  @typescript @worker
  Scenario: Extract modules and declarations from a TypeScript source file
	Given a TypeScript worker request containing a source file with a service class
	When the worker analyzes the request
	Then the response contains the source module and service class symbols

  @typescript @worker
  Scenario: Connect an HTTP request to its owning declaration
	Given a TypeScript worker request containing a function that calls a literal API route
	When the worker analyzes the request
	Then the response contains an HTTP endpoint symbol and an owning call relationship

  @typescript @worker
  Scenario: Report a dynamic HTTP route that cannot be resolved
	Given a TypeScript worker request containing a fetch call with a dynamic route
	When the worker analyzes the request
	Then the response contains a warning for the unresolved HTTP route

  @typescript @worker
  Scenario: Reject a source file outside the analysis root
	Given a TypeScript worker request referencing a source file outside its root
	When the worker analyzes the request
	Then the response reports that the file is outside the analysis root
