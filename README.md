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

## ToDO Module

**Responsabilidade:** CRUD de tarefas em arquivo JSON.
 
**Localização:** `...`
 
**Modelo:**
```json
[
  {
    "id": "a1b2c3",
    "title": "Revisar PR do backend",
    "text": "Desenvolver os testes unitários para o módulo de RAG",
    "criticidade": "alta"
  }
]
```

**Comandos CLI:**
```
/todo add "título" "descrição" 1/2/3/4/5
/todo list ?(1/2/3/4/5)
/todo done <id>
/todo remove <id>
```
 
**Detalhes:**
- `id` → primeiros 6 chars de `Guid.NewGuid()`
- Salvar com `System.Text.Json` formatado (indented)
- O `todos.json` **TAMBÉM** é indexado pelo RAG (é um arquivo na pasta!)
- Então quando o user perguntar "quais minhas tarefas?", o RAG já puxa.

---

## Ollama Module

**Responsabilidade:** Comunicação HTTP com o Ollama local.

> Nas releases, configuramos uma instalação automática do ollama se não existir no sistema e realizamos o pull do modelo llm

**Endpoints usados:**
- `POST http://localhost:11434/api/chat` — gerar respostas
- `POST http://localhost:11434/api/embeddings` — gerar vetores
 
**Fluxo:**
```
[User Input] → OllamaClient.ChatAsync(messages[]) → resposta string
[Texto]      → OllamaClient.EmbedAsync(text) → float[] (vetor)
```
 
**Detalhes técnicos:**
- Usar `HttpClient` singleton (reusar conexão)
- Chat: enviar `{ model, messages, stream: false }` — sem streaming para simplificar
- Embed: enviar `{ model: "nomic-embed-text", prompt: text }` → retorna `embedding: float[]`
- Timeout de 120s (modelos pequenos podem demorar no primeiro load)
 
---























2. Pipeline RAG - TextChunker → CosineSimilarity → VectorStore → FileIndexer.


3. ChatService + SessionManager — orquestra RAG + LLM + histórico de sessão via cache.