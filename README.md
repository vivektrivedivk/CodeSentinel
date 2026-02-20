# CodeSentinel

> A repository-aware architectural intelligence layer on top of a local LLM. The LLM is the brain. The real product is the context engine.

---

## ⚠️ Current State (Phase 1)

CodeSentinel is **not yet a coding assistant**. Right now it is a chat interface — a UI wrapper around a local DeepSeek Coder model running via Ollama.

```
User → Prompt → DeepSeek Coder → Response
```

There is currently **no**:
- File system or repository awareness
- Repo indexing or code chunking
- Vector search or semantic retrieval
- Project structure or architecture mapping
- Symbol extraction or dependency analysis

This is intentional. Phase 1 establishes the full technical foundation — Native AOT backend, streaming pipeline, vector store, SIMD cosine similarity, and SSE-based Blazor frontend — so that the repository intelligence layer can be built on top of a solid base.

---

## 🎯 What CodeSentinel Is Designed to Become

A developer tool that understands your codebase the way a senior engineer would — not by reading every file on every query, but by maintaining a living index of your repository's structure, symbols, and semantics, and injecting the right context at query time.

```
User Query
   ↓
Intent Classifier
   ↓
Architectural question  →  Inject project tree + key files
Code-specific question  →  Run embedding search over indexed chunks
Refactor request        →  Roslyn analysis + semantic retrieval
```

---

## Features

- **Local LLM Integration** — Uses Ollama for fully offline, privacy-focused AI
- **RAG Pipeline** — Architecture in place; retrieval activates once indexing is wired
- **Vector Search** — SIMD-accelerated cosine similarity via `System.Numerics.Vector<float>`
- **Native AOT** — Minimal memory footprint, fast cold start, no reflection at runtime
- **Streaming Responses** — Real-time token streaming via Server-Sent Events
- **Blazor Frontend** — Chat UI with progressive streaming render
- **SQLite Vector Store** — Embedded database for code embeddings, no external dependencies

---

## Tech Stack

- .NET 8 — Native AOT Minimal API (backend)
- .NET 10 — Blazor WebAssembly (frontend)
- Ollama (local LLM runtime)
- SQLite (vector storage via raw ADO.NET)
- `System.Numerics.Vector<T>` (SIMD acceleration)
- Source-generated JSON (`System.Text.Json`)

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — required for the API
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — required for the Blazor frontend
- [Ollama](https://ollama.ai/) installed and running

### Required Ollama Models

```bash
ollama pull nomic-embed-text
ollama pull deepseek-coder:6.7b
```

---

## Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/vivektrivedivk/CodeSentinel.git
   cd CodeSentinel
   ```

2. **Ensure Ollama is running**
   ```bash
   ollama serve
   ```

3. **Run the API**
   ```bash
   cd CodeSentinel.API
   dotnet run
   ```

4. **Run the Web Frontend** *(separate terminal)*
   ```bash
   cd CodeSentinel.Web
   dotnet run
   ```

5. **Open the app**
   - API + Swagger UI: `http://localhost:5000/swagger`
   - Web UI: `http://localhost:5001/chat`

---

## API Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/api/chat` | POST | Non-streaming chat — returns complete response |
| `/api/chat/stream` | POST | Streaming chat via Server-Sent Events |
| `/api/index` | POST | Index a code chunk for RAG retrieval |

### Example Request

```bash
curl -X POST http://localhost:5000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"query": "How does the VectorStore work?", "top_k": 5}'
```

---

## Project Structure

```
CodeSentinel/
├── CodeSentinel.API/
│   ├── Endpoints/             # Minimal API route registration
│   ├── Infrastructure/        # Database initialization
│   ├── Json/                  # AOT-safe source-generated JSON context
│   ├── Models/                # Request/response/domain models
│   ├── Services/              # RAG, LLM, Embedding, VectorStore
│   └── Utilities/             # SIMD vector math
└── CodeSentinel.Web/
    └── Components/Chat/       # Blazor streaming chat UI
```

---

## Configuration

Ollama is expected at `http://localhost:11434` by default.

| Setting | Location |
|---|---|
| LLM model | `Model` constant in `LocalLlmService.cs` |
| Embedding model | `Model` constant in `EmbeddingService.cs` |
| Database path | `VectorStore:Path` in `appsettings.json` |

---

## Memory Budget

Designed to run entirely under 8 GB RAM:

| Component | Approximate Usage |
|---|---|
| Ollama — deepseek-coder Q4 | ~4.0 GB |
| Ollama — nomic-embed-text | ~0.3 GB |
| SQLite + .NET runtime | ~0.3 GB |
| In-process embedding scan buffer | ~0.1 GB (scales with index size) |

---

## 🗺 Roadmap

### Phase 2 — Repository Intelligence (next)

The core of what makes CodeSentinel real. Nothing below requires changing the existing API surface — it slots into the RAG pipeline that is already in place.

**File System Scanner**
Recursively walk a project root, detect relevant file types (`.cs`, `.json`, `.csproj`, `.sln`, `.ts`), and build an in-memory `ProjectMap`. Without this the system is blind to the repository it is supposed to reason about.

```csharp
public interface IProjectIndexer
{
    Task<ProjectMap> IndexAsync(string rootPath);
}

public sealed class ProjectMap
{
    public List<ProjectFile> Files { get; set; } = [];
    public List<string> FolderStructure { get; set; } = [];
}
```

**Code Chunking + Embedding**
Break each file into 300–800 token chunks, generate embeddings via `nomic-embed-text`, and persist them to the SQLite vector store. This enables semantic retrieval — finding the chunk most relevant to a user query, not just keyword matching.

**Project Structure Awareness**
Parse `.csproj` references, extract namespaces, and build a folder hierarchy model. This structural context is injected into the prompt alongside semantic results, giving the LLM awareness of how the system fits together — not just what individual functions do.

---

### Phase 3 — Intent-Aware Query Routing

**Intent Classifier**
Determine whether a query is architectural ("explain the layering"), code-specific ("what does `VectorStore.SearchAsync` do"), or a refactor request ("make this async"). Route to the appropriate context-building strategy rather than always running a full embedding search.

**Roslyn Integration**
For refactor and code-quality queries, use Roslyn to extract symbol trees, method signatures, and call graphs. Combine static analysis output with semantic retrieval for higher-precision answers on code structure questions.

---

### Phase 4 — Repository Session & Incremental Indexing

**Persistent Project Sessions**
Let a user open a repository once and have it remain indexed across sessions. Track file modification timestamps and re-index only changed files on subsequent opens.

**Watch Mode**
File system watcher that triggers incremental re-embedding when source files change during an active session, keeping the index current without a full re-index.

---

## License

MIT — see [LICENSE](LICENSE) for details.

---

## Acknowledgements

- [Ollama](https://ollama.ai/) — local LLM runtime
- [nomic-embed-text](https://ollama.ai/library/nomic-embed-text) — embedding model
- [DeepSeek Coder](https://ollama.ai/library/deepseek-coder) — code generation model