namespace Paperless.Modules.ToDo;

public class TodoManager : ITaskManager
{
    private readonly TodoRepository _repository;

    public TodoManager(TodoRepository repository)
    {
        _repository = repository;
    }

    /* Cria e persiste uma nova tarefa */
    public TodoTask CreateTask(string title, string description, int priority)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required!");

        //todo: Add validaçaõ de description por tamanho de texto e as validações de validade

        var item = new TodoTask(title, description, priority);

        var tasks = _repository.LoadAll();
        tasks.Add(item);
        _repository.SaveAll(tasks);

        return item;
    }

    /* Lista tarefas com filtro opcional de prioridade */
    public List<TodoTask> ListTasks(int? priorityFilter = null)
    {
        var tasks = _repository.LoadAll();

        if (priorityFilter.HasValue)
            tasks = tasks.Where(t => t.Priority == priorityFilter.Value).ToList();

        return tasks
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.IsComplete)
            .ToList();
    }

    /* Marca tarefa como concluída pelo ID */
    public TodoTask? CompleteTask(string id)
    {
        var tasks = _repository.LoadAll();

        var task = tasks.FirstOrDefault(t =>
            t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (task is null)
            return null;

        task.CompleteTask();
        _repository.SaveAll(tasks);

        return task;
    }

    /* Remove tarefa pelo ID */
    public bool DeleteTask(string id)
    {
        var tasks = _repository.LoadAll();

        int removed = tasks.RemoveAll(t =>
            t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
            return false;

        _repository.SaveAll(tasks);
        return true;
    }

    /* Busca tarefa por ID */
    public TodoTask? GetTask(string id)
    {
        var tasks = _repository.LoadAll();

        return tasks.FirstOrDefault(t =>
            t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

}