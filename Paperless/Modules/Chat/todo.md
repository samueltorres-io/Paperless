### 5. ChatService.cs (Orquestrador) <-- 
 
**Responsabilidade:** Conectar o input do user com RAG + sessão + LLM.
 
**Fluxo de uma pergunta:**
```
1. User digita pergunta
2. Gerar embedding da pergunta via OllamaClient.EmbedAsync()
3. Buscar top-3 chunks similares via VectorStore.SearchSimilar()
4. Montar prompt com contexto: RAG + Resumo da sessão + Prompt + Pergunta do user
5. Enviar para Ollama /api/chat
6. Retornar resposta + salvar na sessão
7. Chama funcção para pegar o resumo anterior e adicionar o que foi respondido no ultimo chat, para aumentar o contexto de sessão.
```