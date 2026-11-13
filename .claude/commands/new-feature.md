---
description: Implement a GitHub issue end to end (plan, code, tests, review, PR)
argument-hint: <issue number>
---

Implement GitHub issue #$ARGUMENTS following the BookFlow workflow. Follow CLAUDE.md throughout.

1. **Read the issue** with the GitHub MCP tools. Restate the acceptance criteria in your own words.
2. **Branch:** create `feature/issue-$ARGUMENTS-<short-slug>` from main.
3. **Plan:** list the files you will add or change and why. Show the plan and WAIT for my approval before writing code.
4. **Implement:** if the work adds a new entity or endpoint, use the add-endpoint skill. Keep controllers thin and put business rules in services.
5. **Tests:** delegate to the test-writer agent to cover every acceptance criterion.
6. **Review:** run the code-reviewer agent and the security-checker agent. Fix every "Must fix" and high or medium finding, then re-run them once.
7. **Verify:** run `dotnet build` and `dotnet test`. Both must pass.
8. **Ship:** commit with a clear message, push the branch, and open a pull request with the GitHub MCP tools using .github/pull_request_template.md. Include "Closes #$ARGUMENTS". Give me the PR link.

If anything is ambiguous or a step fails twice, stop and ask me instead of guessing.
