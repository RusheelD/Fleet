# Fleet — Projects, Work Items & AI Chat

## Projects

### Project ↔ Repo Mapping

- **V1:** One Fleet project maps to exactly **one GitHub repo** (1:1).
- **Future:** Support one project spanning **multiple repos** (1:many) for monorepos and microservice architectures.

### Creating a Project

1. User provides a **project title**
2. User selects a **GitHub repo** (from their linked GitHub account)
3. Fleet initializes the project and opens the AI chat interface

## AI Chat

The AI chat is the primary interface for defining what should be built. It operates in three modes depending on what the user has available:

### Mode 1 — Spec from Repository

If the repo already contains spec/design documents, the AI agent **fetches them from the repository**, analyzes them, and generates work items based on the existing specs.

### Mode 2 — Spec from File Upload

If the user has spec documents outside the repo, they can **attach files** to the chat. The AI analyzes the uploaded files and generates work items from them.

### Mode 3 — Conversational Spec Creation

If no spec documents exist, the user **chats back and forth** with the AI to describe the product. Once the user is satisfied with the AI's understanding:

- The AI generates work items
- Optionally, the AI generates spec documents and commits them to the repository

### Chat Behavior

- **Iterative** — The spec and work items can be continuously refined through conversation (like editing code with Copilot)
- **Persistent** — Chat history persists across sessions
- **Multi-session** — Users can start new chats and navigate back to previous ones
- **Contextual** — The AI retains understanding of the project, repo structure, and prior decisions
- **No streaming in MVP** — AI responses are returned as complete messages (not streamed token-by-token). Streaming will be added in a later iteration.

## Work Items

Work items function essentially like **Azure DevOps (ADO) work items**, with room for additional customizability in later iterations.

### Core Properties (based on ADO model)

- Title
- Description
- State (e.g., New, Active, In Progress, Resolved, Closed)
- Priority
- Assigned agents (count or auto-detect)
- Acceptance criteria
- Tags / labels
- Parent / child relationships (hierarchy)
- Linked GitHub issue (optional — can be synced)

### Work Item States

States are **user-settable** (like ADO — users can manually change any state at any time):

| State | Description |
| --- | --- |
| New | Just created, not started |
| Active | Acknowledged, ready to be worked on |
| In Progress | Being worked on (by human or agent) |
| Resolved | Work complete, pending verification |
| Closed | Verified and done |

When agents are working on a task, the state is automatically updated with an **AI indicator**:

- States show an "(AI)" suffix or an agent tag (e.g., "In Progress (AI)") to distinguish agent-driven work from human work
- AI-specific sub-states: Planning (AI), In Progress (AI), Resolved (AI)
- Users can always override the state manually

### Editability

- All work items are **fully editable** after AI generation
- Users can manually create, reorder, re-prioritize, and delete work items
- The AI can suggest changes to work items during chat iteration

> **Note:** Detailed field-level customization (custom fields, workflows, etc.) is a later iteration.

## Pull Request Output

When agents complete a work item, the **agents decide** the PR structure:

- Simple tasks → one PR per work item
- Complex tasks → agents may produce multiple PRs (e.g., one for infra, one for feature code)
- The manager agent coordinates to ensure PRs are coherent and don't conflict

Each PR is opened in the linked GitHub repo with:

- A descriptive title referencing the work item
- A detailed description of changes
- Links back to the Fleet work item
