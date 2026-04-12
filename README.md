# Paperless

**Paperless** é um assistente CLI em C# .NET que roda 100% `offline`, usando `Ollama` como motor LLM.
Ele le arquivos de uma pasta local, vetoriza o conteúdo para RAG, gerencia *TODOs* e mantém sessões com TTL.

---

## Stack Tecnológica

| Componente | Tecnologia | Justificativa |
|---|---|---|
| Runtime | .NET 10 | Cross-platform, AOT publish, single binary |
| LLM Engine | Ollama (background) | Já roda localmente, API HTTP simples |
| Modelo Chat | `phi3:mini` | Leve e roda bem em Hardware simples |
| Modelo Embedding | `nomic-embed-text` | 274MB, rápido, boa qualidade |
| Banco Vetorial | SQLite + cosseno em C# | Zero dependência nativa, cross-platform |
| File Watcher | `System.IO.FileSystemWatcher` | Nativo do .NET, event-driven |
| CLI Framework | `System.Console` puro | Sem dependência extra |

---

## Arquitetura de Componentes

```
┌─────────────────────────────────────────────────────┐
│                    CLI REPL Loop                     │
│              (input → process → output)              │
├──────────┬──────────┬───────────────┬───────────────┤
│  Chat    │  TODO    │  RAG Search   │  Session      │
│  Service │  Manager │  Engine       │  Manager      │
├──────────┴──────────┴───────┬───────┴───────────────┤
│                             │                        │
│      Ollama HTTP Client     │   Vector Store (SQLite) │
│    /api/chat + /api/embed   │   embeddings + chunks   │
├─────────────────────────────┴────────────────────────┤
│              File Watcher + Indexer                   │
│        (monitora pasta → chunka → vetoriza)           │
└──────────────────────────────────────────────────────┘
```

---

