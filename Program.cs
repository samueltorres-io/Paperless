using System;
using Paperless.Modules.ToDo;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Paperless — Testes do módulo ToDo ===\n");

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var tasksPath = Path.Combine(homeDir, "Paperless", "tasks.json");

        Console.WriteLine($"Arquivo de tarefas: {tasksPath}\n");

        var repository = new TodoRepository(tasksPath);
        var manager = new TodoManager(repository);

        Console.WriteLine("--- TESTE 1: Criar tarefas ---");

        var task1 = manager.CreateTask("Estudar C#", "Aprender System.Text.Json", 3);
        Console.WriteLine($"Criada: {task1}");

        var task2 = manager.CreateTask("Configurar Ollama", "Instalar phi3:mini", 4);
        Console.WriteLine($"Criada: {task2}");

        var task3 = manager.CreateTask("Comprar café", "Café especial pro escritório", 1);
        Console.WriteLine($"Criada: {task3}");

        Console.WriteLine();

        Console.WriteLine("--- TESTE 2: Listar todas ---");

        var allTasks = manager.ListTasks();
        foreach (var t in allTasks)
            Console.WriteLine(t);

        Console.WriteLine();

        Console.WriteLine("--- TESTE 3: Filtrar por prioridade 4 (urgent) ---");

        var urgentes = manager.ListTasks(priorityFilter: 4);
        foreach (var t in urgentes)
            Console.WriteLine(t);

        Console.WriteLine();

        Console.WriteLine("--- TESTE 4: Buscar por ID ---");

        var found = manager.GetTask(task1.Id);
        Console.WriteLine($"Buscando {task1.Id}: {found?.ToString() ?? "Não encontrada!"}");

        var notFound = manager.GetTask("id_invalido");
        Console.WriteLine($"Buscando id_invalido: {notFound?.ToString() ?? "Não encontrada!"}");

        Console.WriteLine();

        Console.WriteLine("--- TESTE 5: Completar tarefa ---");

        var completed = manager.CompleteTask(task1.Id);
        Console.WriteLine($"Completada: {completed}");

        var check = manager.GetTask(task1.Id);
        Console.WriteLine($"Verificação: IsComplete = {check?.IsComplete}");

        Console.WriteLine();

        Console.WriteLine("--- TESTE 6: Deletar tarefa ---");

        bool deleted = manager.DeleteTask(task3.Id);
        Console.WriteLine($"Deletou '{task3.Title}': {deleted}");

        bool deletedAgain = manager.DeleteTask(task3.Id);
        Console.WriteLine($"Deletou de novo (mesmo ID): {deletedAgain}");

        Console.WriteLine();

        Console.WriteLine("--- TESTE 7: Estado final ---");

        var finalTasks = manager.ListTasks();
        Console.WriteLine($"Total de tarefas: {finalTasks.Count}");
        foreach (var t in finalTasks)
            Console.WriteLine(t);

        Console.WriteLine();

        Console.WriteLine("--- TESTE 8: Validação de título vazio ---");

        try
        {
            manager.CreateTask("", "Descrição qualquer", 2);
            Console.WriteLine("FALHOU — deveria ter lançado exceção!");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"OK — Exceção capturada: {ex.Message}");
        }

        Console.WriteLine();

        Console.WriteLine("=== Todos os testes concluídos! ===");
    }
}