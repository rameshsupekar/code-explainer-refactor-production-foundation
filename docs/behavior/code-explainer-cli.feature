Feature: Analyze a repository and document its architecture
  As a developer exploring an unfamiliar repository
  I want analysis results, behavior, and architecture exported
  So that I can understand the code and its runtime boundaries

  @dotnet @pipeline
  Scenario: Analyze a solution and export the selected diagrams
	Given a solution containing a C# project and source file
	When I analyze the solution with the dependency diagram selected
	Then the architecture summary identifies the solution and project
	And the dependency diagram is written to the output directory

  @dotnet @pipeline
  Scenario: Analyze a repository containing TypeScript and C# sources
	Given a repository containing supported C# and TypeScript source files
	When I analyze the repository with the TypeScript worker available
	Then symbols from both source ecosystems are included in the model

  @dotnet @pipeline
  Scenario: Report an empty analysis scope without inventing application behavior
	Given an empty repository
	When I analyze the repository with the architecture diagram selected
	Then the result contains a no-supported-source diagnostic
	And the architecture summary reports that no source files were analyzed

  @dotnet @pipeline
  Scenario: Reject an unsupported output format
	Given a repository containing a C# source file
	When I analyze the repository using an unsupported output format
	Then analysis fails with an unsupported-format validation error

  @dotnet @pipeline
  Scenario: Identify infrastructure from repository evidence without exposing configuration values
	Given a repository containing application configuration and infrastructure manifests
	When I analyze the repository with the architecture diagram selected
	Then the architecture summary identifies evidenced technologies and resources
	And configuration values are not copied into the summary

  @dotnet @pipeline
  Scenario: Treat inferred endpoint scenarios as review-required drafts
	Given a repository containing a discoverable API endpoint
	When I analyze the repository with the behavior diagram selected
	Then a Gherkin endpoint scenario draft is generated
	And the draft is marked as requiring review rather than an authoritative business requirement
