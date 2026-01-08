# Contributing to MozaiksCore

Thank you for your interest in contributing to MozaiksCore! This document provides guidelines for contributing.

## Ways to Contribute

### 1. Report Bugs
- Use GitHub Issues
- Include reproduction steps
- Provide system information
- Share error messages/logs

### 2. Suggest Features
- Open a GitHub Discussion first
- Explain the use case
- Consider backwards compatibility
- Be open to feedback

### 3. Submit Code
- Fork the repository
- Create a feature branch
- Write tests for new functionality
- Follow code style guidelines
- Submit a pull request

### 4. Improve Documentation
- Fix typos or clarify confusing sections
- Add examples
- Translate documentation
- Write tutorials

### 5. Create Example Plugins
- Share your plugins in `backend/plugins/`
- Include comprehensive README
- Add tests
- Document configuration options

## Development Setup

```bash
# Fork and clone
git clone https://github.com/YOUR_USERNAME/mozaiks-core.git
cd mozaiks-core

# Install dependencies
pip install -r requirements.txt
pip install -r requirements-dev.txt
npm install

# Run tests
pytest backend/tests/
npm test

# Run linters
flake8 backend/
eslint src/
```

## Code Style

### Python
- Follow PEP 8
- Use type hints
- Write docstrings (Google style)
- Maximum line length: 100 characters

### TypeScript/JavaScript
- Use ESLint configuration
- Prefer TypeScript over JavaScript
- Use async/await over promises
- Document public APIs with JSDoc

## Testing

### Unit Tests
- Write tests for all new functionality
- Aim for >80% code coverage
- Use pytest for Python
- Use Jest for TypeScript

### Integration Tests
- Test plugin loading
- Test auth flows
- Test payment integration

## Pull Request Process

1. **Update Documentation** - Reflect changes in README/docs
2. **Add Tests** - Ensure new code is tested
3. **Run Linters** - Fix all linting errors
4. **Update Changelog** - Add entry for your changes
5. **Request Review** - Tag maintainers for review

## Commit Messages

Use conventional commits:

```
feat: add user profile plugin
fix: resolve auth token expiration issue
docs: update plugin development guide
test: add tests for payment integration
```

## Code Review

- Be respectful and constructive
- Focus on the code, not the person
- Explain reasoning for suggestions
- Be open to alternative approaches

## Community Guidelines

- Be welcoming and inclusive
- Respect differing opinions
- Accept constructive criticism
- Focus on what's best for the project

## Questions?

- Open a GitHub Discussion
- Join our Discord community
- Email: support@mozaiks.com

Thank you for contributing! 🙏
