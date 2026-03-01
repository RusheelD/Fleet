# Fleet — Product Vision & Overview

## Mission

Fleet consolidates work-item management with development. Anyone who wants to build a product — whether they have an engineering team or not — should be able to use Fleet to holistically build their product.

## Target Users

- Solo developers and founders who want AI to act as their engineering team
- Engineering teams looking to accelerate delivery by delegating tasks to AI agents
- Anyone who wants to go from product idea to working code without managing every implementation detail

## Core Workflow

1. **Sign Up / Sign In** — User creates a Fleet account and chooses a plan (one free tier, multiple paid tiers).
2. **Link GitHub** — User connects their GitHub account to Fleet via OAuth.
3. **Create Project** — User provides a project title and selects an existing GitHub repo (linked to the project).
4. **Define Product Spec** — An in-browser AI chat window lets the user describe their product specification conversationally.
5. **Generate Work Items** — The AI generates a holistic set of work items for the product based on the spec.
6. **Assign Agents** — For each work item, the user can:
   - Manually assign a specific number of AI agents, or
   - Let the system auto-detect how many agents are needed.
7. **Agent Execution** — A manager agent assigns roles to the allocated agents, who then break down and complete the task in parallel.
8. **Pull Request** — Each completed task produces a pull request in the linked GitHub repo.

## Agent Model

Fleet uses a **hybrid multi-agent approach**:

| Scenario | Behavior |
| --- | --- |
| **Single agent** | One agent handles the entire task end-to-end (plan → code → PR). |
| **Multiple agents** | A **manager agent** decomposes the task, assigns specialized roles, and coordinates parallel execution. |
| **Auto-detect** | The system determines the optimal number of agents based on task complexity. |
| **User-directed** | The user can manually set agent count per work item. |

Agents are organized in a **manager → worker** hierarchy. The manager:

- Analyzes the work item
- Breaks it into sub-tasks
- Assigns roles to worker agents
- Coordinates parallel execution
- Merges results into a single PR

## GitHub Integration

Fleet integrates deeply with GitHub across the following capabilities:

- **Read issues & PRs** — Agents can read issue descriptions, PR diffs, and existing code
- **Create branches & PRs** — Agents push code to feature branches and open pull requests
- **Run CI checks** — Agents trigger and monitor GitHub Actions workflows for validation
- **Manage projects/boards** — Agents update project boards, labels, and milestones to reflect progress
- **Code review** — Agents leave review comments or approve PRs

> **Not in scope (initially):** Repository creation — Fleet works with existing repos.
