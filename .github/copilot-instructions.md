## Operator Interaction
- Always check the existing files to determine what the patterns and conventions are.
- For every question, take into account the current project as context.
- For every request, validate that the modifications will produce the expected output prior to applying any change.
- For every question or request, if there is anything unclear, ask for clarifications.
- For every question or request, if there is anything that cannot be validated, report it back prior to apply any modifications.
- When asked to fix code, first explain the problems found.
- When asked to generate tests, first explain what tests will be created.
- When making multiple changes, provide a step-by-step overview first.

## Security
- Check the code for vulnerabilities after generating.
- Avoid hardcoding sensitive information like credentials or API keys.
- Use secure coding practices and validate all inputs.

## Environment Variables
- If a .env file exists, use it for local environment variables.
- Document any new environment variables in README.md.
- Provide example values in .env.example.

## Version Control
- Keep commits atomic and focused on single changes.
- Follow conventional commit message format.
- Update .gitignore for new build artifacts or dependencies.

## Code guidelines
- Follow existing project code style and conventions.
- Use C# conventions.
- Optimize for functionality / performance.
- Add type hints and docstrings for all new functions.
- Include comments for complex logic.

## Testing Requirements
- Include unit tests for new functionality.
- Maintain minimum 80% code coverage.
- Add integration tests for API endpoints.

## Change Logging
- Each time you generete code, note the changes in changelog.md.
- Follow semantic versioning guidelines.
- Include date and description of changes.