<div align="center">

```
██████╗  █████╗ ██████╗ ███████╗██████╗ ██╗     ███████╗███████╗███████╗
██╔══██╗██╔══██╗██╔══██╗██╔════╝██╔══██╗██║     ██╔════╝██╔════╝██╔════╝
██████╔╝███████║██████╔╝█████╗  ██████╔╝██║     █████╗  ███████╗███████╗
██╔═══╝ ██╔══██║██╔═══╝ ██╔══╝  ██╔══██╗██║     ██╔══╝  ╚════██║╚════██║
██║     ██║  ██║██║     ███████╗██║  ██║███████╗███████╗███████║███████║
╚═╝     ╚═╝  ╚═╝╚═╝     ╚══════╝╚═╝  ╚═╝╚══════╝╚══════╝╚══════╝╚══════╝
```

**Seu assistente pessoal, 100% offline. Zero nuvem. Zero telemetria. Zero concessões.**

[![.NET](https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Ollama](https://img.shields.io/badge/Ollama-000000?style=for-the-badge&logo=ollama&logoColor=white)](https://ollama.com/)
[![SQLite](https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![Platform](https://img.shields.io/badge/Platform-Linux%20%7C%20Windows%20%7C%20macOS-blue?style=for-the-badge)]()

</div>

---

## 🧠 O que é o Paperless?

**Paperless** é uma **LLM local e privada via CLI**, escrita em **C# .NET 10**, que roda inteiramente na sua máquina — sem precisar de internet, sem enviar dados para servidores externos, sem assinaturas.

A ideia nasceu de uma necessidade simples: ter um **assistente inteligente** que conhece seus arquivos, seus projetos e suas tarefas — e que responde perguntas sobre eles de forma direta, rápida e **completamente offline**.

> *"Como ter o seu próprio ChatGPT particular, que só lê o que você autorizar e nunca fala com ninguém."*

---

## ✨ Funcionalidades

- 🔍 **RAG (Retrieval-Augmented Generation)** — indexa sua pasta local e usa **busca semântica por cosseno** para encontrar os trechos mais relevantes antes de responder
- 📋 **Gerenciador de TODO** — crie, liste e conclua tarefas via CLI; o arquivo de tarefas também é indexado pelo RAG automaticamente
- 💬 **Sessão com TTL** — mantém contexto da conversa em memória com expiração de **10 minutos** por inatividade
- 👁️ **File Watcher** — monitora a pasta em tempo real; novos arquivos são **vetorizados automaticamente** sem reiniciar
- 🏃 **Single Binary** — compilável como executável único via **AOT publish** do .NET
- 🔒 **100% Privado** — nenhum dado sai da sua máquina. Jamais.

---

## 🏗️ Arquitetura

```
┌─────────────────────────────────────────────────────────────┐
│                      CLI REPL Loop                          │
│               (input → process → output)                    │
├───────────┬────────────┬─────────────────┬──────────────────┤
│  Chat     │  TODO      │   RAG Search    │  Session         │
│  Service  │  Manager   │   Engine        │  Manager (TTL)   │
├───────────┴────────────┴────────┬────────┴──────────────────┤
│                                 │                            │
│     Ollama HTTP Client          │  Vector Store (SQLite)     │
│  /api/chat · /api/embeddings    │  embeddings + chunks       │
├─────────────────────────────────┴────────────────────────────┤
│                  File Watcher + Indexer                      │
│          (monitora pasta → chunka → vetoriza)                │
└──────────────────────────────────────────────────────────────┘
```

O fluxo de uma pergunta segue 6 etapas:

1. **Verifica sessão** — expirou? Limpa o contexto e recomeça
2. **Gera embedding** da pergunta via `nomic-embed-text`
3. **Busca RAG** — recupera os top-K chunks com maior similaridade cosseno
4. **Monta o prompt** — injeta o contexto semântico no system prompt
5. **Chama o Ollama** — obtém a resposta do modelo de chat
6. **Atualiza o resumo** da sessão via chamada extra ao LLM

---

## 🛠️ Stack Tecnológica

| Componente | Tecnologia | Por quê |
|---|---|---|
| Runtime | **.NET 10** | Cross-platform, AOT publish, single binary |
| LLM Engine | **Ollama** | Roda localmente, API HTTP simples |
| Modelo Chat | `qwen3.5:0.8b` | Leve, roda bem em hardware modesto |
| Modelo Embedding | `nomic-embed-text` | 274MB, rápido, excelente qualidade semântica |
| Banco Vetorial | **SQLite + cosseno em C#** | Zero dependência nativa, cross-platform |
| File Watcher | `FileSystemWatcher` | Nativo do .NET, event-driven |
| CLI | `System.Console` puro | Sem dependências extras |

---

## 📦 Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com/) instalado e rodando localmente
- Modelos baixados:

```bash
ollama pull qwen3.5:0.8b
ollama pull nomic-embed-text
```

> **Nota:** Em releases futuras, a instalação do Ollama e o pull dos modelos serão feitos automaticamente pelo instalador.

---

## 🚀 Como usar

### 1. Clone e configure

```bash
git clone https://github.com/seu-usuario/paperless.git
cd paperless
```

Edite o `appsettings.json` para apontar para a sua pasta de dados:

```json
{
  "Storage": {
    "BaseFolder": "Paperless",
    "UserFolderPath": "data/"
  },
  "Ollama": {
    "Model": "qwen3.5:0.8b",
    "EmbeddingModel": "nomic-embed-text"
  }
}
```

### 2. Execute

```bash
dotnet run
```

### 3. Converse

```
❯ Quais são minhas tarefas de alta prioridade?
❯ O que eu anotei sobre o projeto X?
❯ /todo add "Revisar PR" "Checar os testes unitários" 1
❯ /todo list
❯ /todo done a1b2c3
```

---

## 📋 Comandos TODO

| Comando | Descrição |
|---|---|
| `/todo add "título" "descrição" <1-5>` | Cria uma nova tarefa com prioridade |
| `/todo list` | Lista todas as tarefas |
| `/todo list <1-5>` | Filtra por prioridade |
| `/todo done <id>` | Marca tarefa como concluída |
| `/todo remove <id>` | Remove uma tarefa |

> As tarefas ficam salvas em `tasks.json` e são **automaticamente indexadas pelo RAG** — você pode perguntar sobre elas em linguagem natural.

---

## 🗂️ Estrutura de Pastas

```
~/Paperless/
├── system/
│   ├── tasks.json        ← suas tarefas (TODO)
│   └── paperless.db      ← banco vetorial SQLite
└── data/                 ← sua pasta monitorada
    ├── projetos/
    ├── ideias.md
    └── notas.txt
```

---

## 🔐 Privacidade — a promessa

**Nenhum dado sai da sua máquina.**

- Sem analytics, sem telemetria, sem logs remotos
- O modelo roda via **Ollama local** — sem chamadas para OpenAI, Anthropic ou qualquer API externa
- Seus arquivos são indexados localmente no **SQLite** — nada é enviado para nenhum servidor
- Funciona **100% offline** após o setup inicial

---

## 🗺️ Roadmap

- [x] RAG com busca semântica por cosseno
- [x] Gerenciador de TODO com CLI
- [x] Sessão em memória com TTL
- [x] File Watcher com reindexação automática
- [x] Instalador automático do Ollama
- [x] Pull automático dos modelos na primeira execução
- [x] Instalador Windows/Linux/Mac
- [ ] Suporte a PDFs e imagens na indexação
- [x] Interface TUI (Terminal UI) com [Spectre.Console](https://spectreconsole.net/)
- [ ] Exportação de TODO para Markdown

---

## 🤝 Contribuindo

Contribuições são bem-vindas! Abra uma **issue** para bugs ou sugestões, ou envie um **pull request** diretamente.

---

<div align="center">

Feito com ☕ e a vontade de não depender de nuvem nenhuma.

**[⬆ Voltar ao topo](#)**

</div>