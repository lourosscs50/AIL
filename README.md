# A.I.L. — AI Intelligence Layer

A.I.L. (AI Intelligence Layer) is a modular platform for managing, validating, and executing AI-driven workflows with strict architectural boundaries.

It is designed to ensure that AI remains a controlled dependency—not a source of unpredictable business logic—by enforcing structure, validation, and observability across all AI interactions.

---

## 🧠 Core Philosophy

- AI is a dependency, not the core
- Core systems produce truth; A.I.L. produces intelligence
- Context is the edge, not the model
- No domain logic leakage into the core
- Deterministic behavior over implicit behavior

A.I.L. exists to make AI **reliable, auditable, and controllable** inside real systems.

---

## 🏗 Architecture Overview

A.I.L. follows a **modular monolith** architecture with strict internal boundaries.

### Key Modules

- **Execution**
  - Orchestrates AI execution requests
  - Integrates provider selection and reliability layers

- **Prompt Registry**
  - Manages versioned prompts
  - Enforces variable contracts and validation
  - Supports lifecycle operations (create, activate, deactivate, promote)
  - Deterministic resolution (highest active version wins)

- **Provider Registry**
  - Abstracts AI providers and models
  - Validates provider/model combinations before execution

- **Reliability**
  - Handles retries, fallbacks, and failure behavior
  - Ensures execution resilience across providers

- **Policy Registry**
  - Defines rules governing execution behavior

- **Context Engine**
  - Supplies structured context to execution requests

- **Observability**
  - Tracks execution metadata without leaking sensitive content

- **Audit**
  - Records execution events for traceability

---

## 🔐 Prompt Registry Design

The Prompt Registry is a core component of A.I.L.

### Features

- Versioned prompts (`v1`, `v2`, `v1.1`, etc.)
- Active/inactive state management
- Strict variable contract enforcement:
  - Required variables must be provided
  - Unknown variables are rejected
- Deterministic resolution:
  - Explicit version → exact match required
  - No version → highest active version selected

### Lifecycle Operations

- `CreatePromptVersionAsync`
- `ActivatePromptVersionAsync`
- `DeactivatePromptVersionAsync`
- `PromotePromptVersionAsync`

### Persistence

Prompt definitions are stored via `IPromptDefinitionRepository`.

Current implementation includes:
- In-memory repository (for testing/dev)
- File-based repository (durable local persistence)

---

## ⚙️ Execution Flow

1. Request enters Execution module
2. Prompt is resolved via Prompt Registry
3. Provider + model selected via Provider Registry
4. Reliability layer applies retry/fallback policy
5. Execution occurs through provider abstraction
6. Observability and Audit record metadata (no prompt leakage)

---

## 🧪 Testing

- Execution tests validate end-to-end behavior
- Prompt registry tests validate:
  - version resolution
  - lifecycle operations
  - variable contract enforcement
- Architecture tests ensure boundary integrity

---

## 🚧 Current Status

Foundation phase complete:

- Prompt registry with lifecycle and validation
- Repository-backed design
- File-based persistence
- Provider abstraction and reliability layer
- Clean execution integration
- Passing builds and tests

---

## 🔭 Roadmap

Next phases:

- Durable persistence expansion (beyond file storage)
- Prompt lifecycle management interfaces
- Memory Core (stateful intelligence layer)
- Decision Engine (policy-driven intelligence selection)
- Control Plane (system-wide orchestration and observability)

---

## 🧩 Design Principles

- Prefer explicit over implicit behavior
- Fail fast on invalid inputs
- Keep AI usage observable but secure
- Maintain strict separation of concerns
- Build foundation before expansion

---

## 📌 Summary

A.I.L. is not an AI application.

It is the system that **controls how AI is used**—ensuring that intelligence is structured, validated, and scalable across real-world systems.
